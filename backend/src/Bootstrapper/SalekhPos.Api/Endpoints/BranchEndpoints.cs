using System.Globalization;
using SalekhPos.Access.Application;
using SalekhPos.Access.Infrastructure;

namespace SalekhPos.Api.Endpoints;

public static class BranchEndpoints
{
    public static void MapBranchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches")
            .RequireAuthorization().RequireRateLimiting("business");
        group.MapGet("", async (Guid organizationId, HttpContext context, BranchAccessReader reader, CancellationToken cancellationToken) =>
        {
            if (!TryQuery(context, out var pageSize, out var after) || organizationId == Guid.Empty)
            {
                return InvalidQuery();
            }
            var identity = Identity(context);
            var page = await reader.ReadAsync(identity, organizationId, pageSize, after, null, cancellationToken);
            return Results.Ok(page);
        });
        group.MapGet("/{branchId:guid}", async (Guid organizationId, Guid branchId, HttpContext context, BranchAccessReader reader, CancellationToken cancellationToken) =>
        {
            if (organizationId == Guid.Empty || branchId == Guid.Empty)
            {
                return InvalidQuery();
            }
            var page = await reader.ReadAsync(Identity(context), organizationId, 1, null, branchId, cancellationToken);
            return page.Items.Count == 0
                ? Results.Problem(statusCode: 404, title: "Branch is unavailable", extensions: new Dictionary<string, object?> { ["code"] = "branch_unavailable" })
                : Results.Ok(page.Items[0]);
        });
    }

    private static AccessIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

    private static IResult InvalidQuery() => Results.Problem(statusCode: 400, title: "The branch query is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_query" });

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
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture, out pageSize) || pageSize is < 1 or > 100))
        {
            return false;
        }
        if (query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var id) || id == Guid.Empty)
            {
                return false;
            }
            after = id;
        }
        return true;
    }
}
