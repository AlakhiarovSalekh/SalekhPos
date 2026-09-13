using System.Net;
using SalekhPos.Desktop.Application.Devices;
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
    public void RuntimeSettingsLoadRequiresDeploymentOrganizationOnly()
    {
        var values = ValidValues();
        values["SALEKHPOS_BRANCH_ID"] = "not-trusted";
        values["SALEKHPOS_DEVICE_ID"] = "not-trusted";

        var settings = DesktopRuntimeSettings.Load(name => values.GetValueOrDefault(name));

        Assert.Equal(new Uri("https://pos.test/"), settings.ApiBaseAddress);
        Assert.Equal("https://identity.test", settings.Oidc.Authority);
        Assert.Equal(["openid", "profile", "salekhpos-api"], settings.Oidc.Scopes);
        Assert.Equal(Guid.Parse(values[DesktopRuntimeSettings.OrganizationIdVariable]!), settings.OrganizationId);
        Assert.True(Path.IsPathFullyQualified(settings.DatabasePath));
    }

    [Fact]
    public void RuntimeSettingsRejectMissingOrganization()
    {
        var values = ValidValues();
        values.Remove(DesktopRuntimeSettings.OrganizationIdVariable);

        Assert.Throws<InvalidOperationException>(() =>
            DesktopRuntimeSettings.Load(name => values.GetValueOrDefault(name)));
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
            "cashier-1", "refresh-token"));
        var scope = Scope(settings);
        var workspace = new Workspace(scope);
        var factory = new WorkspaceFactory(workspace, () => oidc.Completed);
        var terminal = new CapturingHandler();
        var tokens = new InMemoryAccessTokenProvider();
        var deviceFactory = new DeviceRuntimeFactory(scope);
        using var bootstrap = new DesktopRuntimeBootstrap(settings, oidc, tokens, factory, () => terminal,
            deviceFactory);

        using var flow = await bootstrap.SignInAsync();
        var runtime = Assert.IsType<DesktopAuthenticatedRuntime>(flow);

        Assert.True(oidc.Completed);
        Assert.True(factory.CreatedAfterSignIn);
        Assert.Same(workspace, runtime.Workspace);
        Assert.Equal("cashier-1", runtime.Subject);
        Assert.Equal(scope, factory.Scope);
        Assert.Equal(settings.DatabasePath, factory.DatabasePath);
        Assert.Equal(0, deviceFactory.Runtime.DiscoveryCalls);
        using var response = await factory.Client!.GetAsync("api/v1/probe");
        Assert.Equal("Bearer", terminal.AuthorizationScheme);
        Assert.Equal("access-token", terminal.AuthorizationParameter);
    }

    [Fact]
    public async Task AuthenticatedApiRequestTransparentlyUsesRenewedBearerToken()
    {
        var settings = Settings();
        var now = DateTimeOffset.UtcNow;
        var clock = new AdjustableTimeProvider(now);
        var oidc = new OidcClient(
            new NativeOidcSession("old-access", now.AddMinutes(1), "cashier-1", "old-refresh"),
            new NativeOidcSession("new-access", now.AddMinutes(10), "cashier-1", "new-refresh"));
        var scope = Scope(settings);
        var factory = new WorkspaceFactory(new Workspace(scope));
        var terminal = new CapturingHandler();
        var tokens = new InMemoryAccessTokenProvider(clock);
        using var bootstrap = new DesktopRuntimeBootstrap(settings, oidc, tokens, factory, () => terminal,
            new DeviceRuntimeFactory(scope));
        using var runtime = await bootstrap.SignInAsync();
        clock.Advance(TimeSpan.FromSeconds(31));

        using var response = await factory.Client!.GetAsync("api/v1/probe");

        Assert.Equal("new-access", terminal.AuthorizationParameter);
        Assert.Equal(1, oidc.RefreshCalls);
    }

    [Fact]
    public async Task FailedSignInDoesNotConstructWorkspaceOrRetainCredentials()
    {
        var settings = Settings();
        var tokens = new InMemoryAccessTokenProvider();
        var scope = Scope(settings);
        var factory = new WorkspaceFactory(new Workspace(scope));
        using var bootstrap = new DesktopRuntimeBootstrap(settings, new OidcClient(null), tokens, factory,
            deviceRuntimeFactory: new DeviceRuntimeFactory(scope));

        await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.SignInAsync());

        Assert.Equal(0, factory.Calls);
        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() => tokens.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task CompositionFailureAndRuntimeDisposalClearCredentials()
    {
        var settings = Settings();
        var tokens = new InMemoryAccessTokenProvider();
        var oidc = new OidcClient(new NativeOidcSession("access-token", DateTimeOffset.UtcNow.AddMinutes(5),
            "cashier-1", "refresh-token"));
        using (var failedBootstrap = new DesktopRuntimeBootstrap(settings, oidc, tokens,
                   new ThrowingWorkspaceFactory(), deviceRuntimeFactory: new DeviceRuntimeFactory(Scope(settings))))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => failedBootstrap.SignInAsync());
            await Assert.ThrowsAsync<ReauthenticationRequiredException>(() => tokens.GetAccessTokenAsync(default));
        }

        var successfulTokens = new InMemoryAccessTokenProvider();
        using var bootstrap = new DesktopRuntimeBootstrap(settings, oidc, successfulTokens,
            new WorkspaceFactory(new Workspace(Scope(settings))),
            deviceRuntimeFactory: new DeviceRuntimeFactory(Scope(settings)));
        var runtime = await bootstrap.SignInAsync();
        Assert.Equal("access-token", await successfulTokens.GetAccessTokenAsync(default));

        runtime.Dispose();

        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() =>
            successfulTokens.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task RuntimeSignOutClearsCredentialsAndAllowsFreshAuthentication()
    {
        var settings = Settings();
        var tokens = new InMemoryAccessTokenProvider();
        var oidc = new SequencedOidcClient(
            new NativeOidcSession("access-1", DateTimeOffset.UtcNow.AddMinutes(5), "cashier-1", "refresh-1"),
            new NativeOidcSession("access-2", DateTimeOffset.UtcNow.AddMinutes(5), "cashier-2", "refresh-2"));
        using var bootstrap = new DesktopRuntimeBootstrap(settings, oidc, tokens,
            new WorkspaceFactory(new Workspace(Scope(settings))),
            deviceRuntimeFactory: new DeviceRuntimeFactory(Scope(settings)));

        var first = await bootstrap.SignInAsync();
        Assert.Equal("cashier-1", first.Subject);
        first.Dispose();
        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() =>
            tokens.GetAccessTokenAsync(default));

        using var second = await bootstrap.SignInAsync();
        Assert.Equal("cashier-2", second.Subject);
        Assert.Equal("access-2", await tokens.GetAccessTokenAsync(default));
        Assert.Equal(2, oidc.SignInCalls);
    }

    [Fact]
    public async Task FirstTimeSetupProvisionsSelectedAssignmentAndCreatesTrustedWorkspace()
    {
        var settings = Settings();
        var branch = new DesktopBranch(Guid.NewGuid(), settings.OrganizationId, Guid.NewGuid(), "B1", "Central",
            "Asia/Tbilisi");
        var register = new DesktopRegister(Guid.NewGuid(), branch.Id, "R1", "Front", true,
            DateTimeOffset.UtcNow);
        var trustedDeviceId = Guid.NewGuid();
        var devices = new SetupDeviceRuntime(branch, register, trustedDeviceId);
        var factory = new WorkspaceFactory(new Workspace(new PosWorkspaceScope(settings.OrganizationId,
            branch.Id, trustedDeviceId)));
        var tokens = new InMemoryAccessTokenProvider();
        using var bootstrap = new DesktopRuntimeBootstrap(settings,
            new OidcClient(new NativeOidcSession("access", DateTimeOffset.UtcNow.AddMinutes(5), "cashier", "refresh")),
            tokens, factory, deviceRuntimeFactory: new SetupDeviceRuntimeFactory(devices));
        using var flow = await bootstrap.SignInAsync();
        var setup = Assert.IsType<DesktopDeviceSetupSession>(flow);

        var registers = await setup.GetActiveRegistersAsync(branch.Id);
        using var runtime = await setup.ProvisionAsync(branch.Id, registers[0].Id, "POS-01", "Front terminal");

        Assert.Equal(1, devices.ProvisionCalls);
        Assert.Equal(new PosWorkspaceScope(settings.OrganizationId, branch.Id, trustedDeviceId), factory.Scope);
        Assert.Equal(CurrentPlatform(), devices.Request!.Platform);
        Assert.Equal(1, devices.Request.SyncProtocolVersion);
    }

    [Fact]
    public async Task FailedOrCancelledSetupDoesNotPublishWorkspaceAndCanRetry()
    {
        var settings = Settings();
        var branch = new DesktopBranch(Guid.NewGuid(), settings.OrganizationId, Guid.NewGuid(), "B1", "Central",
            "Asia/Tbilisi");
        var register = new DesktopRegister(Guid.NewGuid(), branch.Id, "R1", "Front", true,
            DateTimeOffset.UtcNow);
        var devices = new SetupDeviceRuntime(branch, register, Guid.NewGuid()) { FailNextProvision = true };
        var factory = new WorkspaceFactory(new Workspace(Scope(settings)));
        using var bootstrap = new DesktopRuntimeBootstrap(settings,
            new OidcClient(new NativeOidcSession("access", DateTimeOffset.UtcNow.AddMinutes(5), "cashier", "refresh")),
            new InMemoryAccessTokenProvider(), factory,
            deviceRuntimeFactory: new SetupDeviceRuntimeFactory(devices));
        using var flow = await bootstrap.SignInAsync();
        var setup = Assert.IsType<DesktopDeviceSetupSession>(flow);
        await setup.GetActiveRegistersAsync(branch.Id);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            setup.ProvisionAsync(branch.Id, register.Id, "POS-01", "Front terminal"));
        Assert.Equal(0, factory.Calls);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            setup.ProvisionAsync(branch.Id, register.Id, "POS-01", "Front terminal", cancelled.Token));
        Assert.Equal(0, factory.Calls);

        using var runtime = await setup.ProvisionAsync(branch.Id, register.Id, "POS-01", "Front terminal");
        Assert.Equal(1, factory.Calls);
    }

    private static Dictionary<string, string?> ValidValues() => new(StringComparer.Ordinal)
    {
        [DesktopRuntimeSettings.ApiBaseAddressVariable] = "https://pos.test/",
        [DesktopRuntimeSettings.OidcAuthorityVariable] = "https://identity.test",
        [DesktopRuntimeSettings.OidcClientIdVariable] = "salekhpos-desktop",
        [DesktopRuntimeSettings.OidcScopesVariable] = "openid profile salekhpos-api",
        [DesktopRuntimeSettings.OidcCallbackPortVariable] = "49152",
        [DesktopRuntimeSettings.OrganizationIdVariable] = "11111111-1111-4111-8111-111111111111",
        [DesktopRuntimeSettings.DatabasePathVariable] = Path.Combine(Path.GetTempPath(), "salekhpos-tests.db"),
    };

    private static DesktopRuntimeSettings Settings()
    {
        var values = ValidValues();
        return DesktopRuntimeSettings.Load(name => values.GetValueOrDefault(name));
    }

    private static PosWorkspaceScope Scope(DesktopRuntimeSettings settings) => new(settings.OrganizationId,
        Guid.Parse("22222222-2222-4222-8222-222222222222"),
        Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static string CurrentPlatform() => OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux"
        : throw new PlatformNotSupportedException();

    private sealed class OidcClient(NativeOidcSession? session, NativeOidcSession? refreshed = null)
        : INativeOidcClient
    {
        public bool Completed { get; private set; }
        public int RefreshCalls { get; private set; }
        public Task<NativeOidcSession> SignInAsync(NativeOidcSettings settings,
            CancellationToken cancellationToken = default)
        {
            if (session is null) throw new InvalidOperationException("Sign-in failed.");
            Completed = true;
            return Task.FromResult(session);
        }

        public Task<NativeOidcSession> RefreshAsync(NativeOidcSettings settings, string refreshToken,
            string expectedSubject, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            if (refreshed is null) throw new NotSupportedException();
            Assert.Equal(session!.RefreshToken, refreshToken);
            Assert.Equal(session.Subject, expectedSubject);
            return Task.FromResult(refreshed);
        }
    }

    private sealed class SequencedOidcClient(params NativeOidcSession[] sessions) : INativeOidcClient
    {
        private int next;
        public int SignInCalls { get; private set; }

        public Task<NativeOidcSession> SignInAsync(NativeOidcSettings settings,
            CancellationToken cancellationToken = default)
        {
            SignInCalls++;
            return Task.FromResult(sessions[next++]);
        }

        public Task<NativeOidcSession> RefreshAsync(NativeOidcSettings settings, string refreshToken,
            string expectedSubject, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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

    private sealed class DeviceRuntimeFactory(PosWorkspaceScope? resumedScope) : IDesktopDeviceRuntimeFactory
    {
        public DeviceRuntime Runtime { get; } = new(resumedScope);
        public IDesktopDeviceRuntime Create(string databasePath, HttpClient authenticatedClient) => Runtime;
    }

    private sealed class DeviceRuntime(PosWorkspaceScope? resumedScope) : IDesktopDeviceRuntime
    {
        public int DiscoveryCalls { get; private set; }
        public Task<PosWorkspaceScope?> TryResumeAsync(Guid organizationId,
            CancellationToken cancellationToken) => Task.FromResult(resumedScope);
        public Task<IReadOnlyList<DesktopBranch>> GetBranchesAsync(Guid organizationId,
            CancellationToken cancellationToken)
        {
            DiscoveryCalls++;
            return Task.FromResult<IReadOnlyList<DesktopBranch>>([]);
        }
        public Task<IReadOnlyList<DesktopRegister>> GetActiveRegistersAsync(Guid organizationId, Guid branchId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DesktopRegister>>([]);
        public Task<ProvisionedDevice> ProvisionAsync(DeviceProvisioningRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class SetupDeviceRuntimeFactory(SetupDeviceRuntime runtime) : IDesktopDeviceRuntimeFactory
    {
        public IDesktopDeviceRuntime Create(string databasePath, HttpClient authenticatedClient) => runtime;
    }

    private sealed class SetupDeviceRuntime(DesktopBranch branch, DesktopRegister register, Guid trustedDeviceId)
        : IDesktopDeviceRuntime
    {
        public bool FailNextProvision { get; set; }
        public int ProvisionCalls { get; private set; }
        public DeviceProvisioningRequest? Request { get; private set; }
        public Task<PosWorkspaceScope?> TryResumeAsync(Guid organizationId,
            CancellationToken cancellationToken) => Task.FromResult<PosWorkspaceScope?>(null);
        public Task<IReadOnlyList<DesktopBranch>> GetBranchesAsync(Guid organizationId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DesktopBranch>>([branch]);
        public Task<IReadOnlyList<DesktopRegister>> GetActiveRegistersAsync(Guid organizationId, Guid branchId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DesktopRegister>>([register]);
        public Task<ProvisionedDevice> ProvisionAsync(DeviceProvisioningRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProvisionCalls++;
            Request = request;
            if (FailNextProvision)
            {
                FailNextProvision = false;
                throw new HttpRequestException("Unavailable.");
            }
            return Task.FromResult(new ProvisionedDevice(trustedDeviceId, request.BranchId, request.RegisterId,
                request.Code, request.Name, request.Platform, "active", 1, DateTimeOffset.UtcNow, "operator",
                null, null, null, null));
        }
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

    private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount) => now = now.Add(amount);
    }

    private sealed class Workspace(PosWorkspaceScope scope) : IPosWorkspace
    {
        public PosWorkspaceState CurrentState { get; } = new(scope, null, true, 0, false, true,
            DateTimeOffset.UtcNow);
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
