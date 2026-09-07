using SalekhPos.SystemAdministration.Contracts;
using SalekhPos.SystemAdministration.Domain;

namespace SalekhPos.SystemAdministration.Application;

public sealed record PrivilegedActor(PlatformIdentity Identity, DateTimeOffset AuthenticatedAt, bool HasMfa);

public interface ISuperAdminRegistry
{
    Task<PlatformAuthority> GetAuthorityAsync(PlatformIdentity identity, CancellationToken cancellationToken);
    Task<SuperAdminResponse> RegisterAsync(PrivilegedActor actor, Guid operationId, PlatformIdentity target,
        string reason, string traceId, CancellationToken cancellationToken);
    Task<SuperAdminResponse> RevokeAsync(PrivilegedActor actor, Guid operationId, Guid targetId,
        string reason, string traceId, CancellationToken cancellationToken);
}

public sealed class SuperAdminAdministration(ISuperAdminRegistry registry, TimeProvider timeProvider)
{
    public Task<PlatformAuthority> GetAuthorityAsync(PlatformIdentity identity, CancellationToken cancellationToken) =>
        registry.GetAuthorityAsync(identity, cancellationToken);

    public Task<SuperAdminResponse> RegisterAsync(PrivilegedActor actor, RegisterSuperAdminRequest request,
        string traceId, CancellationToken cancellationToken)
    {
        RequireMfa(actor);
        return registry.RegisterAsync(actor, PlatformInput.Identifier(request.OperationId),
            new PlatformIdentity(actor.Identity.Issuer, request.Subject), PlatformInput.Text(request.Reason, 1000),
            traceId, cancellationToken);
    }

    public Task<SuperAdminResponse> RevokeAsync(PrivilegedActor actor, Guid targetId, RevokeSuperAdminRequest request,
        string traceId, CancellationToken cancellationToken)
    {
        RequireMfa(actor);
        return registry.RevokeAsync(actor, PlatformInput.Identifier(request.OperationId), PlatformInput.Identifier(targetId),
            PlatformInput.Text(request.Reason, 1000), traceId, cancellationToken);
    }

    private void RequireMfa(PrivilegedActor actor)
    {
        if (!actor.HasMfa || !RecentMfa.IsRecent(actor.AuthenticatedAt, timeProvider.GetUtcNow()))
        {
            throw new PlatformAccessDeniedException();
        }
    }
}

public sealed class PlatformAccessDeniedException : Exception;
public sealed class PlatformConflictException : Exception;
public sealed class PlatformUnavailableException : Exception;
