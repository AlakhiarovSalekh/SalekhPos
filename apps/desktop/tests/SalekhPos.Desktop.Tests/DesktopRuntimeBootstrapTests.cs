using System.Net;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Domain.LocalCatalog;
using SalekhPos.Desktop.Domain.LocalSales;
using SalekhPos.Desktop.Infrastructure.Authentication;
using SalekhPos.Desktop.Infrastructure.Configuration;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class DesktopRuntimeBootstrapTests
{
    [Fact]
    public void RuntimeSettingsLoadCompleteExplicitAssignment()
    {
        var values = ValidValues();

        var settings = DesktopRuntimeSettings.Load(name => values.GetValueOrDefault(name));

        Assert.Equal(new Uri("https://pos.test/"), settings.ApiBaseAddress);
        Assert.Equal("https://identity.test", settings.Oidc.Authority);
        Assert.Equal(["openid", "profile", "salekhpos-api"], settings.Oidc.Scopes);
        Assert.Equal(Guid.Parse(values[DesktopRuntimeSettings.OrganizationIdVariable]!),
            settings.Scope.OrganizationId);
        Assert.True(Path.IsPathFullyQualified(settings.DatabasePath));
    }

    [Theory]
    [InlineData(DesktopRuntimeSettings.ApiBaseAddressVariable, "http://pos.test/")]
    [InlineData(DesktopRuntimeSettings.ApiBaseAddressVariable, "https://pos.test/api/")]
    [InlineData(DesktopRuntimeSettings.OidcAuthorityVariable, "http://identity.test")]
    [InlineData(DesktopRuntimeSettings.OrganizationIdVariable, "00000000-0000-0000-0000-000000000000")]
    [InlineData(DesktopRuntimeSettings.DatabasePathVariable, "salekhpos.db")]
    [InlineData(DesktopRuntimeSettings.OidcScopesVariable, "openid offline_access")]
    public void RuntimeSettingsRejectIncompleteOrUnsafeValues(string name, string value)
    {
        var values = ValidValues();
        values[name] = value;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DesktopRuntimeSettings.Load(key => values.GetValueOrDefault(key)));

        Assert.Equal("Desktop runtime configuration is incomplete or unsafe.", exception.Message);
    }

    [Fact]
    public void RuntimeSettingsRejectPublicClientSecret()
    {
        var values = ValidValues();
        values[DesktopRuntimeSettings.OidcClientSecretVariable] = "must-not-be-embedded";

        Assert.Throws<InvalidOperationException>(() =>
            DesktopRuntimeSettings.Load(key => values.GetValueOrDefault(key)));
    }

    [Fact]
    public async Task BootstrapConstructsWorkspaceOnlyAfterValidatedSignInAndAddsBearerPerRequest()
    {
        var settings = Settings();
        var oidc = new OidcClient(new NativeOidcSession("access-token", DateTimeOffset.UtcNow.AddMinutes(5),
            "cashier-1"));
        var workspace = new Workspace(settings.Scope);
        var factory = new WorkspaceFactory(workspace, () => oidc.Completed);
        var terminal = new CapturingHandler();
        var tokens = new InMemoryAccessTokenProvider();
        using var bootstrap = new DesktopRuntimeBootstrap(settings, oidc, tokens, factory, () => terminal);

        using var runtime = await bootstrap.SignInAsync();

        Assert.True(oidc.Completed);
        Assert.True(factory.CreatedAfterSignIn);
        Assert.Same(workspace, runtime.Workspace);
        Assert.Equal("cashier-1", runtime.Subject);
        Assert.Equal(settings.Scope, factory.Scope);
        Assert.Equal(settings.DatabasePath, factory.DatabasePath);
        using var response = await factory.Client!.GetAsync("api/v1/probe");
        Assert.Equal("Bearer", terminal.AuthorizationScheme);
        Assert.Equal("access-token", terminal.AuthorizationParameter);
    }

    [Fact]
    public async Task FailedSignInDoesNotConstructWorkspaceOrRetainCredentials()
    {
        var settings = Settings();
        var tokens = new InMemoryAccessTokenProvider();
        var factory = new WorkspaceFactory(new Workspace(settings.Scope));
        using var bootstrap = new DesktopRuntimeBootstrap(settings, new OidcClient(null), tokens, factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.SignInAsync());

        Assert.Equal(0, factory.Calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tokens.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task CompositionFailureAndRuntimeDisposalClearCredentials()
    {
        var settings = Settings();
        var tokens = new InMemoryAccessTokenProvider();
        var oidc = new OidcClient(new NativeOidcSession("access-token", DateTimeOffset.UtcNow.AddMinutes(5),
            "cashier-1"));
        using (var failedBootstrap = new DesktopRuntimeBootstrap(settings, oidc, tokens,
                   new ThrowingWorkspaceFactory()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => failedBootstrap.SignInAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => tokens.GetAccessTokenAsync(default));
        }

        var successfulTokens = new InMemoryAccessTokenProvider();
        using var bootstrap = new DesktopRuntimeBootstrap(settings, oidc, successfulTokens,
            new WorkspaceFactory(new Workspace(settings.Scope)));
        var runtime = await bootstrap.SignInAsync();
        Assert.Equal("access-token", await successfulTokens.GetAccessTokenAsync(default));

        runtime.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() => successfulTokens.GetAccessTokenAsync(default));
    }

    private static Dictionary<string, string?> ValidValues() => new(StringComparer.Ordinal)
    {
        [DesktopRuntimeSettings.ApiBaseAddressVariable] = "https://pos.test/",
        [DesktopRuntimeSettings.OidcAuthorityVariable] = "https://identity.test",
        [DesktopRuntimeSettings.OidcClientIdVariable] = "salekhpos-desktop",
        [DesktopRuntimeSettings.OidcScopesVariable] = "openid profile salekhpos-api",
        [DesktopRuntimeSettings.OidcCallbackPortVariable] = "49152",
        [DesktopRuntimeSettings.OrganizationIdVariable] = "11111111-1111-4111-8111-111111111111",
        [DesktopRuntimeSettings.BranchIdVariable] = "22222222-2222-4222-8222-222222222222",
        [DesktopRuntimeSettings.DeviceIdVariable] = "33333333-3333-4333-8333-333333333333",
        [DesktopRuntimeSettings.DatabasePathVariable] = Path.Combine(Path.GetTempPath(), "salekhpos-tests.db"),
    };

    private static DesktopRuntimeSettings Settings()
    {
        var values = ValidValues();
        return DesktopRuntimeSettings.Load(name => values.GetValueOrDefault(name));
    }

    private sealed class OidcClient(NativeOidcSession? session) : INativeOidcClient
    {
        public bool Completed { get; private set; }
        public Task<NativeOidcSession> SignInAsync(NativeOidcSettings settings,
            CancellationToken cancellationToken = default)
        {
            if (session is null) throw new InvalidOperationException("Sign-in failed.");
            Completed = true;
            return Task.FromResult(session);
        }
    }

    private sealed class WorkspaceFactory(Workspace workspace, Func<bool>? signInCompleted = null)
        : IDesktopPosWorkspaceFactory
    {
        public int Calls { get; private set; }
        public bool CreatedAfterSignIn { get; private set; }
        public PosWorkspaceScope? Scope { get; private set; }
        public string? DatabasePath { get; private set; }
        public HttpClient? Client { get; private set; }
        public Task<IPosWorkspace> CreateAsync(PosWorkspaceScope scope, string databasePath,
            HttpClient authenticatedClient, CancellationToken cancellationToken)
        {
            Calls++;
            CreatedAfterSignIn = signInCompleted?.Invoke() ?? false;
            Scope = scope;
            DatabasePath = databasePath;
            Client = authenticatedClient;
            return Task.FromResult<IPosWorkspace>(workspace);
        }
    }

    private sealed class ThrowingWorkspaceFactory : IDesktopPosWorkspaceFactory
    {
        public Task<IPosWorkspace> CreateAsync(PosWorkspaceScope scope, string databasePath,
            HttpClient authenticatedClient, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Composition failed.");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class Workspace(PosWorkspaceScope scope) : IPosWorkspace
    {
        public PosWorkspaceState CurrentState { get; } = new(scope, null, 0, false, true, DateTimeOffset.UtcNow);
        public Task<PosWorkspaceState> OpenOnlineAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CurrentState);
        public Task<PosWorkspaceState> OpenOfflineAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CurrentState);
        public Task<LocalSellableItem?> FindByProductAsync(Guid productId, DateTimeOffset at,
            CancellationToken cancellationToken) => Task.FromResult<LocalSellableItem?>(null);
        public Task<LocalSellableItem?> FindByBarcodeAsync(string barcode, DateTimeOffset at,
            CancellationToken cancellationToken) => Task.FromResult<LocalSellableItem?>(null);
        public Task<LocalSaleWriteResult> CompleteCashSaleAsync(CashCheckoutRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PosWorkspaceState> SynchronizeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CurrentState);
        public Task<CashMovementResult> RecordCashMovementAsync(Guid operationId, string kind, decimal amount,
            string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ClosedCashSessionResult> CloseCashSessionAsync(Guid operationId, decimal countedCash,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
