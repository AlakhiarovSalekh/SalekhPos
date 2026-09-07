using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Identity.Application;
using SalekhPos.Identity.Infrastructure;

namespace SalekhPos.Identity.Api;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/identity/revoke-current-token", async (HttpContext context,
            TokenRevocations revocations, CancellationToken cancellationToken) =>
        {
            // No target identity or credential is accepted from the request body.
            if (context.Items[typeof(AuthenticatedCredential)] is not AuthenticatedCredential credential)
            {
                return Results.Unauthorized();
            }
            await revocations.RevokeAsync(credential, context.TraceIdentifier, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("business");
    }
}
