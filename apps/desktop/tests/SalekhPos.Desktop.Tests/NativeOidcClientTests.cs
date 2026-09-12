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
        Assert.Equal("openid profile salekhpos-api offline_access", query["scope"]);
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

        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() => tokens.GetAccessTokenAsync(default));
        tokens.SetSession(new NativeOidcSession("short-lived", clock.GetUtcNow().AddSeconds(30), "operator"));
        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() => tokens.GetAccessTokenAsync(default));
        tokens.SetSession(new NativeOidcSession("active", clock.GetUtcNow().AddMinutes(5), "operator"));

        Assert.Equal("active", await tokens.GetAccessTokenAsync(default));
        tokens.Clear();
        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() => tokens.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task InMemoryProviderAtomicallyRotatesRefreshTokens()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));
        var tokens = new InMemoryAccessTokenProvider(clock);
        var refreshTokens = new List<string>();
        tokens.SetSession(new NativeOidcSession("access-0", clock.GetUtcNow().AddSeconds(20), "operator",
            "refresh-0"), (refreshToken, subject, _) =>
        {
            refreshTokens.Add(refreshToken);
            var version = refreshTokens.Count;
            return Task.FromResult(new NativeOidcSession($"access-{version}",
                clock.GetUtcNow().AddMinutes(5), subject, $"refresh-{version}"));
        });

        Assert.Equal("access-1", await tokens.GetAccessTokenAsync(default));
        clock.Advance(TimeSpan.FromMinutes(4.5));
        Assert.Equal("access-2", await tokens.GetAccessTokenAsync(default));
        Assert.Equal(["refresh-0", "refresh-1"], refreshTokens);
    }

    [Fact]
    public async Task ConcurrentRequestsShareOneRefresh()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));
        var tokens = new InMemoryAccessTokenProvider(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NativeOidcSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        tokens.SetSession(new NativeOidcSession("old-access", clock.GetUtcNow().AddSeconds(20), "operator",
            "old-refresh"), async (_, _, _) =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            return await release.Task;
        });

        var requests = Enumerable.Range(0, 12).Select(_ => tokens.GetAccessTokenAsync(default)).ToArray();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, Volatile.Read(ref calls));
        release.SetResult(new NativeOidcSession("new-access", clock.GetUtcNow().AddMinutes(5), "operator",
            "new-refresh"));

        Assert.All(await Task.WhenAll(requests), token => Assert.Equal("new-access", token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task InvalidRefreshClearsAllCredentials()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));
        var tokens = new InMemoryAccessTokenProvider(clock);
        var calls = 0;
        tokens.SetSession(new NativeOidcSession("old-access", clock.GetUtcNow().AddSeconds(20), "operator",
            "old-refresh"), (_, subject, _) =>
        {
            calls++;
            return Task.FromResult(new NativeOidcSession("new-access", clock.GetUtcNow().AddMinutes(5), subject,
                "old-refresh"));
        });

        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() => tokens.GetAccessTokenAsync(default));
        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() => tokens.GetAccessTokenAsync(default));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CallerCancellationDoesNotCancelOrCorruptSharedRefresh()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));
        var tokens = new InMemoryAccessTokenProvider(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NativeOidcSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sharedRefreshToken = CancellationToken.None;
        tokens.SetSession(new NativeOidcSession("old-access", clock.GetUtcNow().AddSeconds(20), "operator",
            "old-refresh"), async (_, _, cancellationToken) =>
        {
            Assert.True(cancellationToken.CanBeCanceled);
            sharedRefreshToken = cancellationToken;
            entered.SetResult();
            return await release.Task;
        });
        using var cancellation = new CancellationTokenSource();

        var canceledRequest = tokens.GetAccessTokenAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledRequest);
        Assert.False(sharedRefreshToken.IsCancellationRequested);
        release.SetResult(new NativeOidcSession("new-access", clock.GetUtcNow().AddMinutes(5), "operator",
            "new-refresh"));

        Assert.Equal("new-access", await tokens.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task ClearCancelsUnderlyingRefreshWithoutCorruptingReplacementSession()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));
        var tokens = new InMemoryAccessTokenProvider(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tokens.SetSession(new NativeOidcSession("old-access", clock.GetUtcNow().AddSeconds(20), "operator",
            "old-refresh"), async (_, _, cancellationToken) =>
        {
            entered.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The canceled refresh unexpectedly continued.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                canceled.SetResult();
                throw;
            }
        });

        var oldRefresh = tokens.GetAccessTokenAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        tokens.Clear();
        tokens.SetSession(new NativeOidcSession("replacement-access", clock.GetUtcNow().AddMinutes(5),
            "operator"));

        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() => oldRefresh);
        Assert.Equal("replacement-access", await tokens.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task RefreshResponseMustContainANewRotatedRefreshToken()
    {
        using var rsa = RSA.Create(2048);
        var handler = new RefreshResponseHandler(rsa);
        using var client = new HttpClient(handler);
        var oidc = new NativeOidcClient(client, new CapturingBrowser(),
            new FixedCallback(new AuthorizationCallback(null, null, null)));
        var settings = new NativeOidcSettings("https://identity.test", "desktop-client",
            ["openid", "profile", "salekhpos-api"], 49152);

        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() =>
            oidc.RefreshAsync(settings, "old-refresh", "operator"));

        Assert.Contains("grant_type=refresh_token", handler.TokenRequest, StringComparison.Ordinal);
        Assert.Contains("client_id=desktop-client", handler.TokenRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret", handler.TokenRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("scope=", handler.TokenRequest, StringComparison.Ordinal);
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

    private sealed class RefreshResponseHandler(RSA rsa) : HttpMessageHandler
    {
        public string TokenRequest { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var parameters = rsa.ExportParameters(false);
            object document;
            if (request.RequestUri!.AbsolutePath.EndsWith("openid-configuration", StringComparison.Ordinal))
            {
                document = new
                {
                    issuer = "https://identity.test",
                    authorization_endpoint = "https://identity.test/authorize",
                    token_endpoint = "https://identity.test/token",
                    jwks_uri = "https://identity.test/keys",
                };
            }
            else if (request.RequestUri.AbsolutePath.EndsWith("keys", StringComparison.Ordinal))
            {
                document = new
                {
                    keys = new[] { new { kty = "RSA", kid = "test", use = "sig", alg = "RS256",
                        n = Base64UrlEncoder.Encode(parameters.Modulus), e = Base64UrlEncoder.Encode(parameters.Exponent) } },
                };
            }
            else
            {
                TokenRequest = await request.Content!.ReadAsStringAsync(cancellationToken);
                document = new
                {
                    access_token = "new-access",
                    refresh_token = "old-refresh",
                    token_type = "Bearer",
                    expires_in = 300,
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(document), Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount) => now = now.Add(amount);
    }
}
