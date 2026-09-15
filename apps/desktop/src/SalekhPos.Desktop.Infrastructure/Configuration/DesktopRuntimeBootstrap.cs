using System.Collections.Concurrent;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Application.Operations;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Infrastructure.Authentication;
using SalekhPos.Desktop.Infrastructure.Operations;

namespace SalekhPos.Desktop.Infrastructure.Configuration;

public interface IDesktopPosWorkspaceFactory
{
    Task<IPosWorkspace> CreateAsync(PosWorkspaceScope scope, string databasePath,
        HttpClient authenticatedClient, CancellationToken cancellationToken);
}

public sealed class DesktopPosWorkspaceFactory : IDesktopPosWorkspaceFactory
{
    public async Task<IPosWorkspace> CreateAsync(PosWorkspaceScope scope, string databasePath,
        HttpClient authenticatedClient, CancellationToken cancellationToken) =>
        await DesktopPosComposition.CreateAsync(scope, databasePath, authenticatedClient, cancellationToken);
}

public interface IDesktopAuthenticatedFlow : IDisposable
{
    string Subject { get; }
}

public sealed class DesktopAuthenticatedRuntime : IDesktopAuthenticatedFlow
{
    private InMemoryAccessTokenProvider? tokens;
    private HttpClient? authenticatedClient;
    private Action? released;

    internal DesktopAuthenticatedRuntime(IPosWorkspace workspace, string subject,
        InMemoryAccessTokenProvider tokens, HttpClient authenticatedClient, Action released)
    {
        Workspace = workspace;
        ManagerOperations = new HttpManagerOperations(authenticatedClient);
        Subject = subject;
        this.tokens = tokens;
        this.authenticatedClient = authenticatedClient;
        this.released = released;
    }

    public IPosWorkspace Workspace { get; }
    public IManagerOperations ManagerOperations { get; }
    public string Subject { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref tokens, null)?.Clear();
        Interlocked.Exchange(ref authenticatedClient, null)?.Dispose();
        Interlocked.Exchange(ref released, null)?.Invoke();
    }
}

public sealed class DesktopDeviceSetupSession : IDesktopAuthenticatedFlow
{
    private readonly Guid organizationId;
    private readonly string databasePath;
    private readonly IDesktopPosWorkspaceFactory workspaceFactory;
    private readonly IDesktopDeviceRuntime devices;
    private readonly Dictionary<Guid, DesktopBranch> permittedBranches;
    private readonly ConcurrentDictionary<Guid, IReadOnlyDictionary<Guid, DesktopRegister>> loadedRegisters = [];
    private readonly Lock ownershipGate = new();
    private InMemoryAccessTokenProvider? tokens;
    private HttpClient? authenticatedClient;
    private Action? released;
    private int operationStarted;

    internal DesktopDeviceSetupSession(Guid organizationId, string databasePath, string subject,
        IReadOnlyList<DesktopBranch> branches, IDesktopPosWorkspaceFactory workspaceFactory,
        IDesktopDeviceRuntime devices, InMemoryAccessTokenProvider tokens, HttpClient authenticatedClient,
        Action released)
    {
        this.organizationId = organizationId;
        this.databasePath = databasePath;
        this.workspaceFactory = workspaceFactory;
        this.devices = devices;
        this.tokens = tokens;
        this.authenticatedClient = authenticatedClient;
        this.released = released;
        Subject = subject;
        Branches = branches;
        permittedBranches = branches.ToDictionary(branch => branch.Id);
    }

    public string Subject { get; }
    public IReadOnlyList<DesktopBranch> Branches { get; }

    public async Task<IReadOnlyList<DesktopRegister>> GetActiveRegistersAsync(Guid branchId,
        CancellationToken cancellationToken = default)
    {
        EnsureActive();
        if (!permittedBranches.ContainsKey(branchId)) throw new ArgumentException("The branch is not permitted.");
        var registers = await devices.GetActiveRegistersAsync(organizationId, branchId, cancellationToken);
        loadedRegisters[branchId] = registers.ToDictionary(register => register.Id);
        return registers;
    }

    public async Task<DesktopAuthenticatedRuntime> ProvisionAsync(Guid branchId, Guid registerId,
        string code, string name, CancellationToken cancellationToken = default)
    {
        EnsureActive();
        if (Interlocked.Exchange(ref operationStarted, 1) != 0)
            throw new InvalidOperationException("A device setup operation is already running.");
        try
        {
            if (!permittedBranches.ContainsKey(branchId)
                || !loadedRegisters.TryGetValue(branchId, out var registers)
                || !registers.ContainsKey(registerId))
                throw new ArgumentException("The selected assignment is invalid.");
            var request = new DeviceProvisioningRequest(organizationId, branchId, registerId, code, name,
                CurrentPlatform(), 1);
            DeviceProvisioner.ValidateRequest(request);
            var device = await devices.ProvisionAsync(request, cancellationToken);
            if (device.Id == Guid.Empty || device.BranchId != branchId || device.RegisterId != registerId
                || device.Status != "active")
                throw new InvalidOperationException("The trusted device assignment is invalid.");
            var scope = new PosWorkspaceScope(organizationId, branchId, device.Id);
            var client = authenticatedClient ?? throw new ObjectDisposedException(nameof(DesktopDeviceSetupSession));
            var workspace = await workspaceFactory.CreateAsync(scope, databasePath, client, cancellationToken);
            return Transfer(workspace);
        }
        catch
        {
            Interlocked.Exchange(ref operationStarted, 0);
            throw;
        }
    }

    public void Dispose()
    {
        lock (ownershipGate)
        {
            tokens?.Clear();
            authenticatedClient?.Dispose();
            released?.Invoke();
            tokens = null;
            authenticatedClient = null;
            released = null;
        }
    }

    private DesktopAuthenticatedRuntime Transfer(IPosWorkspace workspace)
    {
        lock (ownershipGate)
        {
            var ownedTokens = tokens ?? throw new ObjectDisposedException(nameof(DesktopDeviceSetupSession));
            var client = authenticatedClient
                ?? throw new ObjectDisposedException(nameof(DesktopDeviceSetupSession));
            var release = released ?? throw new ObjectDisposedException(nameof(DesktopDeviceSetupSession));
            tokens = null;
            authenticatedClient = null;
            released = null;
            return new DesktopAuthenticatedRuntime(workspace, Subject, ownedTokens, client, release);
        }
    }

    private void EnsureActive()
    {
        lock (ownershipGate)
        {
            ObjectDisposedException.ThrowIf(authenticatedClient is null, this);
        }
    }

    private static string CurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macos";
        if (OperatingSystem.IsLinux()) return "linux";
        throw new PlatformNotSupportedException("Desktop device provisioning is unsupported on this platform.");
    }
}

public sealed class DesktopRuntimeBootstrap : IDisposable
{
    private readonly DesktopRuntimeSettings settings;
    private readonly INativeOidcClient oidcClient;
    private readonly InMemoryAccessTokenProvider tokens;
    private readonly IDesktopPosWorkspaceFactory workspaceFactory;
    private readonly IDesktopDeviceRuntimeFactory deviceRuntimeFactory;
    private readonly Func<HttpMessageHandler> handlerFactory;
    private readonly IDisposable? ownedOidcResources;
    private int signInStarted;
    private bool disposed;

    public DesktopRuntimeBootstrap(DesktopRuntimeSettings settings, INativeOidcClient oidcClient,
        InMemoryAccessTokenProvider tokens, IDesktopPosWorkspaceFactory workspaceFactory,
        Func<HttpMessageHandler>? handlerFactory = null,
        IDesktopDeviceRuntimeFactory? deviceRuntimeFactory = null)
        : this(settings, oidcClient, tokens, workspaceFactory,
            deviceRuntimeFactory ?? new DesktopDeviceRuntimeFactory(),
            handlerFactory ?? (() => new HttpClientHandler()), null)
    {
    }

    private DesktopRuntimeBootstrap(DesktopRuntimeSettings settings, INativeOidcClient oidcClient,
        InMemoryAccessTokenProvider tokens, IDesktopPosWorkspaceFactory workspaceFactory,
        IDesktopDeviceRuntimeFactory deviceRuntimeFactory, Func<HttpMessageHandler> handlerFactory,
        IDisposable? ownedOidcResources)
    {
        this.settings = settings;
        this.oidcClient = oidcClient;
        this.tokens = tokens;
        this.workspaceFactory = workspaceFactory;
        this.deviceRuntimeFactory = deviceRuntimeFactory;
        this.handlerFactory = handlerFactory;
        this.ownedOidcResources = ownedOidcResources;
        settings.Validate();
    }

    public static DesktopRuntimeBootstrap CreateDefault(DesktopRuntimeSettings settings)
    {
        settings.Validate();
        var backchannel = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        return new DesktopRuntimeBootstrap(settings,
            new NativeOidcClient(backchannel, new SystemBrowser(), new LoopbackAuthorizationCallbackReceiver()),
            new InMemoryAccessTokenProvider(), new DesktopPosWorkspaceFactory(), new DesktopDeviceRuntimeFactory(),
            () => new HttpClientHandler(), backchannel);
    }

    public async Task<IDesktopAuthenticatedFlow> SignInAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Interlocked.Exchange(ref signInStarted, 1) != 0)
            throw new InvalidOperationException("A desktop sign-in has already been started.");

        HttpClient? authenticatedClient = null;
        try
        {
            var session = await oidcClient.SignInAsync(settings.Oidc, cancellationToken);
            ValidateSession(session);
            tokens.SetSession(session, (refreshToken, subject, token) =>
                oidcClient.RefreshAsync(settings.Oidc, refreshToken, subject, token));
            authenticatedClient = new HttpClient(new BearerTokenHandler(tokens)
            {
                InnerHandler = handlerFactory(),
            })
            {
                BaseAddress = settings.ApiBaseAddress,
                Timeout = TimeSpan.FromSeconds(30),
            };
            var devices = deviceRuntimeFactory.Create(settings.DatabasePath, authenticatedClient);
            var scope = await devices.TryResumeAsync(settings.OrganizationId, cancellationToken);
            if (scope is not null)
            {
                var workspace = await workspaceFactory.CreateAsync(scope, settings.DatabasePath,
                    authenticatedClient, cancellationToken);
                var runtime = new DesktopAuthenticatedRuntime(workspace, session.Subject, tokens,
                    authenticatedClient, ReleaseSignIn);
                authenticatedClient = null;
                return runtime;
            }

            var branches = await devices.GetBranchesAsync(settings.OrganizationId, cancellationToken);
            var setup = new DesktopDeviceSetupSession(settings.OrganizationId, settings.DatabasePath,
                session.Subject, branches, workspaceFactory, devices, tokens, authenticatedClient, ReleaseSignIn);
            authenticatedClient = null;
            return setup;
        }
        catch
        {
            tokens.Clear();
            authenticatedClient?.Dispose();
            ReleaseSignIn();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        tokens.Dispose();
        ownedOidcResources?.Dispose();
    }

    private void ReleaseSignIn() => Interlocked.Exchange(ref signInStarted, 0);

    private static void ValidateSession(NativeOidcSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(session.AccessToken) || session.AccessToken.Length > 16384
            || session.AccessToken.Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(session.Subject)
            || session.Subject.Length > 512 || session.Subject.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(session.RefreshToken) || session.RefreshToken.Length > 32768
            || session.RefreshToken.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            || session.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
            throw new InvalidOperationException("The authenticated session is invalid.");
    }
}
