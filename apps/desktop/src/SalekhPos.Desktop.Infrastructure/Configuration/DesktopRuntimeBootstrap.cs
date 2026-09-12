using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Infrastructure.Authentication;

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

public sealed class DesktopAuthenticatedRuntime : IDisposable
{
    private InMemoryAccessTokenProvider? tokens;
    private HttpClient? authenticatedClient;

    internal DesktopAuthenticatedRuntime(IPosWorkspace workspace, string subject,
        InMemoryAccessTokenProvider tokens, HttpClient authenticatedClient)
    {
        Workspace = workspace;
        Subject = subject;
        this.tokens = tokens;
        this.authenticatedClient = authenticatedClient;
    }

    public IPosWorkspace Workspace { get; }
    public string Subject { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref tokens, null)?.Clear();
        Interlocked.Exchange(ref authenticatedClient, null)?.Dispose();
    }
}

public sealed class DesktopRuntimeBootstrap : IDisposable
{
    private readonly DesktopRuntimeSettings settings;
    private readonly INativeOidcClient oidcClient;
    private readonly InMemoryAccessTokenProvider tokens;
    private readonly IDesktopPosWorkspaceFactory workspaceFactory;
    private readonly Func<HttpMessageHandler> handlerFactory;
    private readonly IDisposable? ownedOidcResources;
    private int signInStarted;
    private bool disposed;

    public DesktopRuntimeBootstrap(DesktopRuntimeSettings settings, INativeOidcClient oidcClient,
        InMemoryAccessTokenProvider tokens, IDesktopPosWorkspaceFactory workspaceFactory,
        Func<HttpMessageHandler>? handlerFactory = null)
        : this(settings, oidcClient, tokens, workspaceFactory,
            handlerFactory ?? (() => new HttpClientHandler()), null)
    {
    }

    private DesktopRuntimeBootstrap(DesktopRuntimeSettings settings, INativeOidcClient oidcClient,
        InMemoryAccessTokenProvider tokens, IDesktopPosWorkspaceFactory workspaceFactory,
        Func<HttpMessageHandler> handlerFactory, IDisposable? ownedOidcResources)
    {
        this.settings = settings;
        this.oidcClient = oidcClient;
        this.tokens = tokens;
        this.workspaceFactory = workspaceFactory;
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
            new InMemoryAccessTokenProvider(), new DesktopPosWorkspaceFactory(),
            () => new HttpClientHandler(), backchannel);
    }

    public async Task<DesktopAuthenticatedRuntime> SignInAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Interlocked.Exchange(ref signInStarted, 1) != 0)
            throw new InvalidOperationException("A desktop sign-in has already been started.");

        HttpClient? authenticatedClient = null;
        try
        {
            var session = await oidcClient.SignInAsync(settings.Oidc, cancellationToken);
            ValidateSession(session);
            tokens.SetSession(session);
            authenticatedClient = new HttpClient(new BearerTokenHandler(tokens)
            {
                InnerHandler = handlerFactory(),
            })
            {
                BaseAddress = settings.ApiBaseAddress,
                Timeout = TimeSpan.FromSeconds(30),
            };
            var workspace = await workspaceFactory.CreateAsync(settings.Scope, settings.DatabasePath,
                authenticatedClient, cancellationToken);
            var runtime = new DesktopAuthenticatedRuntime(workspace, session.Subject, tokens, authenticatedClient);
            authenticatedClient = null;
            return runtime;
        }
        catch
        {
            tokens.Clear();
            authenticatedClient?.Dispose();
            Interlocked.Exchange(ref signInStarted, 0);
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        tokens.Clear();
        ownedOidcResources?.Dispose();
    }

    private static void ValidateSession(NativeOidcSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(session.AccessToken) || session.AccessToken.Length > 16384
            || session.AccessToken.Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(session.Subject)
            || session.Subject.Length > 512 || session.Subject.Any(char.IsControl)
            || session.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
            throw new InvalidOperationException("The authenticated session is invalid.");
    }
}
