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
            || Scopes.Contains("offline_access", StringComparer.Ordinal)
            || Scopes.Distinct(StringComparer.Ordinal).Count() != Scopes.Count
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

public sealed record NativeOidcSession(string AccessToken, DateTimeOffset ExpiresAt, string Subject,
    string? RefreshToken = null)
{
    public override string ToString() => nameof(NativeOidcSession);
}

public sealed class ReauthenticationRequiredException : InvalidOperationException
{
    public ReauthenticationRequiredException() : base("Re-authentication is required.")
    {
    }
}

public interface INativeOidcClient
{
    Task<NativeOidcSession> SignInAsync(NativeOidcSettings settings,
        CancellationToken cancellationToken = default);

    Task<NativeOidcSession> RefreshAsync(NativeOidcSettings settings, string refreshToken,
        string expectedSubject, CancellationToken cancellationToken = default) =>
        Task.FromException<NativeOidcSession>(new ReauthenticationRequiredException());
}

public sealed class NativeOidcClient(HttpClient backchannel, ISystemBrowser browser,
    IAuthorizationCallbackReceiver callbackReceiver, TimeProvider? timeProvider = null)
    : INativeOidcClient
{
    private const long MaxTokenResponseBytes = 64 * 1024;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<NativeOidcSession> SignInAsync(NativeOidcSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Validate();
        var configuration = await GetConfigurationAsync(settings, cancellationToken);
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
        var tokens = await SendTokenRequestAsync(request, cancellationToken);
        ValidateTokenResponse(tokens, requireIdToken: true, previousRefreshToken: null);
        var subject = await ValidateIdentityTokenAsync(tokens.IdToken!, settings.ClientId,
            configuration, nonce, expectedSubject: null);

        return new NativeOidcSession(tokens.AccessToken!, timeProvider.GetUtcNow().AddSeconds(tokens.ExpiresIn!.Value),
            subject, tokens.RefreshToken);
    }

    public async Task<NativeOidcSession> RefreshAsync(NativeOidcSettings settings, string refreshToken,
        string expectedSubject, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        if (InvalidToken(refreshToken, 32768) || string.IsNullOrWhiteSpace(expectedSubject)
            || expectedSubject.Length > 512 || expectedSubject.Any(char.IsControl))
            throw new ReauthenticationRequiredException();

        var configuration = await GetConfigurationAsync(settings, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = settings.ClientId,
                ["refresh_token"] = refreshToken,
            }),
        };
        var tokens = await SendTokenRequestAsync(request, cancellationToken);
        ValidateTokenResponse(tokens, requireIdToken: false, previousRefreshToken: refreshToken);
        if (!string.IsNullOrEmpty(tokens.IdToken))
            await ValidateIdentityTokenAsync(tokens.IdToken, settings.ClientId, configuration,
                expectedNonce: null, expectedSubject);

        return new NativeOidcSession(tokens.AccessToken!, timeProvider.GetUtcNow().AddSeconds(tokens.ExpiresIn!.Value),
            expectedSubject, tokens.RefreshToken);
    }

    private async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        NativeOidcSettings settings, CancellationToken cancellationToken)
    {
        var authority = settings.Authority.TrimEnd('/');
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            authority + "/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever(backchannel)
            {
                RequireHttps = true,
            });
        var configuration = await manager.GetConfigurationAsync(cancellationToken);
        ValidateMetadata(authority, configuration);
        return configuration;
    }

    private async Task<TokenResponse> SendTokenRequestAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ReauthenticationRequiredException();

        await response.Content.LoadIntoBufferAsync(MaxTokenResponseBytes, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken)
            ?? throw new ReauthenticationRequiredException();
    }

    private static async Task<string> ValidateIdentityTokenAsync(string idToken, string clientId,
        OpenIdConnectConfiguration configuration, string? expectedNonce, string? expectedSubject)
    {
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(idToken,
            new TokenValidationParameters
            {
                ValidIssuer = configuration.Issuer,
                ValidAudience = clientId,
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
        if (!validation.IsValid || validation.SecurityToken is not JsonWebToken token
            || (expectedNonce is not null
                && !string.Equals(token.GetClaim("nonce")?.Value, expectedNonce, StringComparison.Ordinal))
            || string.IsNullOrWhiteSpace(token.Subject)
            || (expectedSubject is not null && !string.Equals(token.Subject, expectedSubject, StringComparison.Ordinal)))
            throw new ReauthenticationRequiredException();
        return token.Subject;
    }

    private static void ValidateTokenResponse(TokenResponse tokens, bool requireIdToken,
        string? previousRefreshToken)
    {
        if (InvalidToken(tokens.AccessToken, 16384) || InvalidToken(tokens.RefreshToken, 32768)
            || tokens.ExpiresIn is < 60 or > 86400
            || !string.Equals(tokens.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase)
            || (requireIdToken && InvalidToken(tokens.IdToken, 32768))
            || (!string.IsNullOrEmpty(tokens.IdToken) && InvalidToken(tokens.IdToken, 32768))
            || (tokens.Scope is not null && (tokens.Scope.Length > 2048 || tokens.Scope.Any(char.IsControl)))
            || (previousRefreshToken is not null
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(previousRefreshToken),
                    Encoding.UTF8.GetBytes(tokens.RefreshToken!))))
            throw new ReauthenticationRequiredException();
    }

    private static bool InvalidToken(string? value, int maximumLength) => string.IsNullOrWhiteSpace(value)
        || value.Length > maximumLength
        || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));

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
            ["scope"] = string.Join(' ', settings.Scopes.Append("offline_access")),
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
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("id_token")] string? IdToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string? TokenType,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: System.Text.Json.Serialization.JsonPropertyName("scope")] string? Scope);
}

public sealed class InMemoryAccessTokenProvider(TimeProvider? timeProvider = null) : IAccessTokenProvider, IDisposable
{
    private readonly Lock sync = new();
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private NativeOidcSession? session;
    private Func<string, string, CancellationToken, Task<NativeOidcSession>>? renew;
    private CancellationTokenSource? sessionLifetime;
    private Task<string>? refreshTask;
    private long generation;
    private bool disposed;

    public void SetSession(NativeOidcSession value) => SetSession(value, null);

    public void SetSession(NativeOidcSession value,
        Func<string, string, CancellationToken, Task<NativeOidcSession>>? renewal)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateSession(value, requireRefreshToken: false);
        var replacementLifetime = new CancellationTokenSource();
        CancellationTokenSource? previousLifetime;
        lock (sync)
        {
            if (disposed)
            {
                replacementLifetime.Dispose();
                ObjectDisposedException.ThrowIf(disposed, this);
            }

            previousLifetime = sessionLifetime;
            session = value;
            renew = renewal;
            sessionLifetime = replacementLifetime;
            refreshTask = null;
            generation++;
        }

        CancelAndDispose(previousLifetime);
    }

    public void Clear()
    {
        CancellationTokenSource? lifetime;
        lock (sync) lifetime = ClearLocked();
        CancelAndDispose(lifetime);
    }

    public void Dispose()
    {
        CancellationTokenSource? lifetime;
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            lifetime = ClearLocked();
        }

        CancelAndDispose(lifetime);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<string>? pendingRefresh = null;
        CancellationTokenSource? lifetimeToCancel = null;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (session is null) throw new ReauthenticationRequiredException();
            if (session.ExpiresAt > timeProvider.GetUtcNow().AddSeconds(30)) return session.AccessToken;
            if (refreshTask is not null)
            {
                pendingRefresh = refreshTask;
            }
            else
            {
                if (InvalidRefreshState(session, renew))
                {
                    lifetimeToCancel = ClearLocked();
                }
                else
                {
                    var capturedSession = session;
                    var capturedRenewal = renew!;
                    var capturedGeneration = generation;
                    var capturedLifetime = sessionLifetime!.Token;
                    pendingRefresh = RefreshCoreAsync(capturedSession, capturedRenewal, capturedGeneration,
                        capturedLifetime);
                    refreshTask = pendingRefresh;
                }
            }
        }

        if (lifetimeToCancel is not null)
        {
            CancelAndDispose(lifetimeToCancel);
            throw new ReauthenticationRequiredException();
        }

        return await pendingRefresh!.WaitAsync(cancellationToken);
    }

    private async Task<string> RefreshCoreAsync(NativeOidcSession previous,
        Func<string, string, CancellationToken, Task<NativeOidcSession>> renewal, long capturedGeneration,
        CancellationToken sessionCancellationToken)
    {
        await Task.Yield();
        try
        {
            var refreshed = await renewal(previous.RefreshToken!, previous.Subject, sessionCancellationToken);
            ValidateSession(refreshed, requireRefreshToken: true);
            if (!string.Equals(refreshed.Subject, previous.Subject, StringComparison.Ordinal)
                || CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(previous.RefreshToken!),
                    Encoding.UTF8.GetBytes(refreshed.RefreshToken!)))
                throw new ReauthenticationRequiredException();

            lock (sync)
            {
                if (disposed || generation != capturedGeneration || !ReferenceEquals(session, previous))
                    throw new ReauthenticationRequiredException();
                session = refreshed;
                generation++;
                refreshTask = null;
                return refreshed.AccessToken;
            }
        }
        catch
        {
            CancellationTokenSource? lifetimeToCancel = null;
            lock (sync)
            {
                if (generation == capturedGeneration) lifetimeToCancel = ClearLocked();
            }
            CancelAndDispose(lifetimeToCancel);
            throw new ReauthenticationRequiredException();
        }
    }

    private static bool InvalidRefreshState(NativeOidcSession value,
        Func<string, string, CancellationToken, Task<NativeOidcSession>>? renewal) => renewal is null
        || InvalidToken(value.RefreshToken, 32768);

    private static void ValidateSession(NativeOidcSession value, bool requireRefreshToken)
    {
        if (InvalidToken(value.AccessToken, 16384) || string.IsNullOrWhiteSpace(value.Subject)
            || value.Subject.Length > 512 || value.Subject.Any(char.IsControl) || value.ExpiresAt == default
            || (requireRefreshToken && InvalidToken(value.RefreshToken, 32768))
            || (!string.IsNullOrEmpty(value.RefreshToken) && InvalidToken(value.RefreshToken, 32768)))
            throw new ReauthenticationRequiredException();
    }

    private static bool InvalidToken(string? value, int maximumLength) => string.IsNullOrWhiteSpace(value)
        || value.Length > maximumLength
        || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));

    private CancellationTokenSource? ClearLocked()
    {
        var lifetime = sessionLifetime;
        session = null;
        renew = null;
        sessionLifetime = null;
        refreshTask = null;
        generation++;
        return lifetime;
    }

    private static void CancelAndDispose(CancellationTokenSource? lifetime)
    {
        if (lifetime is null) return;
        try
        {
            lifetime.Cancel();
        }
        finally
        {
            lifetime.Dispose();
        }
    }
}
