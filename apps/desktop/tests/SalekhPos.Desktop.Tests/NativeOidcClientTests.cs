using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using SalekhPos.Desktop.Infrastructure.Authentication;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class NativeOidcClientTests
{
    [Fact]
    public async Task SignInUsesAuthorizationCodePkceAndRejectsMismatchedState()
    {
        using var rsa = RSA.Create(2048);
        using var client = new HttpClient(new MetadataHandler(rsa));
        var browser = new CapturingBrowser();
        var oidc = new NativeOidcClient(client, browser,
            new FixedCallback(new AuthorizationCallback("authorization-code", "wrong-state", null)));
        var settings = new NativeOidcSettings("https://identity.test", "desktop-client",
            ["openid", "profile", "salekhpos-api"], 49152);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => oidc.SignInAsync(settings));

        Assert.Equal("The authorization response is invalid.", exception.Message);
        Assert.NotNull(browser.Address);
        var query = ParseQuery(browser.Address!.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal(settings.RedirectUri.AbsoluteUri, query["redirect_uri"]);
        Assert.True(query["code_challenge"].Length >= 43);
        Assert.True(query["state"].Length >= 43);
        Assert.True(query["nonce"].Length >= 43);
    }

    [Fact]
    public async Task InMemoryProviderRejectsMissingAndNearlyExpiredSessions()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));
        var tokens = new InMemoryAccessTokenProvider(clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => tokens.GetAccessTokenAsync(default));
        tokens.SetSession(new NativeOidcSession("short-lived", clock.GetUtcNow().AddSeconds(30), "operator"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tokens.GetAccessTokenAsync(default));
        tokens.SetSession(new NativeOidcSession("active", clock.GetUtcNow().AddMinutes(5), "operator"));

        Assert.Equal("active", await tokens.GetAccessTokenAsync(default));
        tokens.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => tokens.GetAccessTokenAsync(default));
    }

    [Theory]
    [InlineData("http://identity.test", "client", 49152)]
    [InlineData("https://identity.test", "", 49152)]
    [InlineData("https://identity.test", "client", 80)]
    public void SettingsRejectUnsafeConfiguration(string authority, string clientId, int port)
    {
        var settings = new NativeOidcSettings(authority, clientId, ["openid"], port);
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?').Split('&')
        .Select(value => value.Split('=', 2)).ToDictionary(value => Uri.UnescapeDataString(value[0]),
            value => Uri.UnescapeDataString(value[1]), StringComparer.Ordinal);

    private sealed class CapturingBrowser : ISystemBrowser
    {
        public Uri? Address { get; private set; }
        public void Open(Uri address) => Address = address;
    }

    private sealed class FixedCallback(AuthorizationCallback callback) : IAuthorizationCallbackReceiver
    {
        public Task<AuthorizationCallback> ReceiveAsync(Uri redirectUri, CancellationToken cancellationToken) =>
            Task.FromResult(callback);
    }

    private sealed class MetadataHandler(RSA rsa) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var parameters = rsa.ExportParameters(false);
            object document = request.RequestUri!.AbsolutePath.EndsWith("openid-configuration", StringComparison.Ordinal)
                ? new
                {
                    issuer = "https://identity.test",
                    authorization_endpoint = "https://identity.test/authorize",
                    token_endpoint = "https://identity.test/token",
                    jwks_uri = "https://identity.test/keys",
                }
                : new
                {
                    keys = new[] { new { kty = "RSA", kid = "test", use = "sig", alg = "RS256",
                        n = Base64UrlEncoder.Encode(parameters.Modulus), e = Base64UrlEncoder.Encode(parameters.Exponent) } },
                };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(document), Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
