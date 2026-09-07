namespace SalekhPos.Identity.Application.Sessions;

public interface ITokenRevocations
{
    Task RevokeAsync(AuthenticatedCredential credential, string traceId, CancellationToken cancellationToken);
}
