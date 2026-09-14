using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Authorization.Application;

namespace SalekhPos.Authorization.Api.Organizations;

public static class OrganizationAccessEndpoints
{
    public static void MapOrganizationAccessEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/access/organizations", async (
            HttpContext context,
            IAccessibleOrganizationReader reader,
            CancellationToken cancellationToken) =>
        {
            if (!TryQuery(context, out var pageSize, out var after))
            {
                return InvalidQuery();
            }

            var identity = new AccessIdentity(
                context.User.FindFirst("iss")!.Value,
                context.User.FindFirst("sub")!.Value);
            return Results.Ok(await reader.ReadAsync(identity, pageSize, after, cancellationToken));
        })
        .RequireAuthorization(AuthorizationPolicies.BusinessApi)
        .RequireRateLimiting("business");
    }

    private static bool TryQuery(HttpContext context, out int pageSize, out Guid? after)
    {
        pageSize = 50;
        after = null;
        var query = context.Request.Query;
        if (query.Keys.Any(key => key is not "pageSize" and not "after"))
        {
            return false;
        }

        if (query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1
                || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture, out pageSize)
                || pageSize is < 1 or > 100))
        {
            return false;
        }

        if (query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor)
                || cursor == Guid.Empty)
            {
                return false;
            }
            after = cursor;
        }

        return true;
    }

    private static IResult InvalidQuery() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "The organization access query is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_organization_access_query" });
}
