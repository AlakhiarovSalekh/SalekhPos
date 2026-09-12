using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SalekhPos.Desktop.Application.Offline;

namespace SalekhPos.Desktop.Infrastructure.Authentication;

public sealed record NativeOidcSettings(string Authority, string ClientId, IReadOnlyList<string> Scopes,
    int CallbackPort)
{
    public Uri RedirectUri => new($"http://127.0.0.1:{CallbackPort}/callback/");

    public void Validate()
    {
        if (!Uri.TryCreate(Authority, UriKind.Absolute, out var authority) || authority.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(authority.UserInfo) || !string.IsNullOrEmpty(authority.Query)
            || !string.IsNullOrEmpty(authority.Fragment) || Authority.Length > 2048
            || string.IsNullOrWhiteSpace(ClientId) || ClientId.Length > 256 || ClientId.Any(char.IsControl)
            || CallbackPort is < 1024 or > 65535 || Scopes.Count is < 1 or > 16
            || !Scopes.Contains("openid", StringComparer.Ordinal)
            || Scopes.Any(scope => string.IsNullOrWhiteSpace(scope) || scope.Length > 128
                || scope.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))))
            throw new InvalidOperationException("Native OIDC configuration is invalid.");
    }
}

public sealed record AuthorizationCallback(string? Code, string? State, string? Error);

public interface ISystemBrowser
{
    void Open(Uri address);
}

public interface IAuthorizationCallbackReceiver
{
    Task<AuthorizationCallback> ReceiveAsync(Uri redirectUri, CancellationToken cancellationToken);
}

public sealed class SystemBrowser : ISystemBrowser
{
    public void Open(Uri address) => Process.Start(new ProcessStartInfo(address.AbsoluteUri)
    {
        UseShellExecute = true,
    });
}

public sealed class LoopbackAuthorizationCallbackReceiver : IAuthorizationCallbackReceiver
{
    public async Task<AuthorizationCallback> ReceiveAsync(Uri redirectUri, CancellationToken cancellationToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri.AbsoluteUri);
        listener.Start();
        var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(5), cancellationToken);
        var query = context.Request.QueryString;
        var callback = new AuthorizationCallback(query["code"], query["state"], query["error"]);
        const string response = "<!doctype html><html><body><p>Authentication completed. You may close this window.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(response);
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        context.Response.Close();
        return callback;
    }
}

public sealed record NativeOidcSession(string AccessToken, DateTimeOffset ExpiresAt, string Subject);

public sealed class NativeOidcClient(HttpClient backchannel, ISystemBrowser browser,
    IAuthorizationCallbackReceiver callbackReceiver, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<NativeOidcSession> SignInAsync(NativeOidcSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Validate();
        var authority = settings.Authority.TrimEnd('/');
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            authority + "/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever(backchannel)
            {
                RequireHttps = true,
            });
        var configuration = await manager.GetConfigurationAsync(cancellationToken);
        ValidateMetadata(authority, configuration);

        var state = RandomUrlSafe(32);
        var nonce = RandomUrlSafe(32);
        var verifier = RandomUrlSafe(64);
        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizationUri = BuildAuthorizationUri(configuration.AuthorizationEndpoint, settings,
            state, nonce, challenge);

        var callbackTask = callbackReceiver.ReceiveAsync(settings.RedirectUri, cancellationToken);
        browser.Open(authorizationUri);
        var callback = await callbackTask;
        if (!string.IsNullOrEmpty(callback.Error)) throw new InvalidOperationException("Authorization was declined.");
        if (string.IsNullOrWhiteSpace(callback.Code) || callback.Code.Length > 4096
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state),
                Encoding.UTF8.GetBytes(callback.State ?? string.Empty)))
            throw new InvalidOperationException("The authorization response is invalid.");

        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = settings.ClientId,
                ["code"] = callback.Code,
                ["redirect_uri"] = settings.RedirectUri.AbsoluteUri,
                ["code_verifier"] = verifier,
            }),
        };
        using var response = await backchannel.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var tokens = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The token response is empty.");
        if (string.IsNullOrWhiteSpace(tokens.AccessToken) || tokens.AccessToken.Length > 16384
            || tokens.AccessToken.Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(tokens.IdToken)
            || tokens.IdToken.Length > 32768 || tokens.ExpiresIn is < 60 or > 86400
            || !string.Equals(tokens.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The token response is invalid.");

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.IdToken,
            new TokenValidationParameters
            {
                ValidIssuer = authority,
                ValidAudience = settings.ClientId,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSsaPssSha256,
                    SecurityAlgorithms.EcdsaSha256],
            });
        if (!validation.IsValid || validation.SecurityToken is not JsonWebToken idToken
            || !string.Equals(idToken.GetClaim("nonce")?.Value, nonce, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(idToken.Subject))
            throw new InvalidOperationException("The identity token is invalid.");

        return new(tokens.AccessToken, _timeProvider.GetUtcNow().AddSeconds(tokens.ExpiresIn), idToken.Subject);
    }

    private static void ValidateMetadata(string authority, OpenIdConnectConfiguration configuration)
    {
        if (!string.Equals(configuration.Issuer.TrimEnd('/'), authority, StringComparison.Ordinal)
            || !ValidHttpsEndpoint(configuration.AuthorizationEndpoint)
            || !ValidHttpsEndpoint(configuration.TokenEndpoint) || configuration.SigningKeys.Count == 0)
            throw new InvalidOperationException("The provider metadata is invalid.");
    }

    private static bool ValidHttpsEndpoint(string endpoint) => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);

    private static Uri BuildAuthorizationUri(string endpoint, NativeOidcSettings settings, string state,
        string nonce, string challenge)
    {
        var values = new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = settings.RedirectUri.AbsoluteUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', settings.Scopes),
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        };
        return new Uri(endpoint + "?" + string.Join('&', values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
    }

    private static string RandomUrlSafe(int bytes) => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(bytes));

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("id_token")] string IdToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
}

public sealed class InMemoryAccessTokenProvider(TimeProvider? timeProvider = null) : IAccessTokenProvider
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private NativeOidcSession? _session;

    public void SetSession(NativeOidcSession session) => _session = session;
    public void Clear() => _session = null;

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = _session;
        if (session is null || session.ExpiresAt <= _timeProvider.GetUtcNow().AddSeconds(30))
            throw new InvalidOperationException("An active sign-in is required.");
        return Task.FromResult(session.AccessToken);
    }
}
