using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Returns.Application.Returns;
using SalekhPos.Returns.Contracts.Returns;

namespace SalekhPos.Returns.Api.Returns;

public static class ReturnEndpoints
{
    public static void MapReturnEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/returns",
            async (Guid organizationId, Guid branchId, HttpContext context, IReturnReader reader,
                CancellationToken cancellationToken) =>
            {
                if (!TryListQuery(context, out var pageSize, out var after)) return InvalidQuery();
                try
                {
                    return Results.Ok(await reader.ListAsync(Identity(context), organizationId, branchId,
                        pageSize, after, cancellationToken));
                }
                catch (ArgumentException) { return InvalidQuery(); }
            }).RequireAuthorization().RequireRateLimiting("business");

        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/returns/{returnId:guid}",
            async (Guid organizationId, Guid branchId, Guid returnId, HttpContext context, IReturnReader reader,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await reader.ReadAsync(Identity(context), organizationId, branchId, returnId,
                        cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ArgumentException) { return InvalidQuery(); }
            }).RequireAuthorization().RequireRateLimiting("business");

        app.MapPost("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/returns",
            async (Guid organizationId, Guid branchId, CompleteReturnRequest request, HttpContext context,
                IReturnCompletion completion, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                    || operationId == Guid.Empty) return Invalid();
                try
                {
                    var result = await completion.CompleteAsync(new(context.User.FindFirst("iss")!.Value,
                        context.User.FindFirst("sub")!.Value), new(organizationId, branchId, Guid.NewGuid(),
                        operationId, request.SaleId, request.Reason, request.Lines), cancellationToken);
                    return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/returns/{result.Return.Id:D}", result.Return)
                        : Results.Ok(result.Return);
                }
                catch (ArgumentException) { return Invalid(); }
            }).RequireAuthorization().RequireRateLimiting("business");
    }
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The return request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_return_request" });
    private static IResult InvalidQuery() => Results.Problem(statusCode: 400, title: "The return query is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_return_query" });
    private static ReturnIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static bool TryListQuery(HttpContext context, out int pageSize, out Guid? after)
    {
        pageSize = 50; after = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "after")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture,
                out pageSize) || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor)
                || cursor == Guid.Empty) return false;
            after = cursor;
        }
        return true;
    }
}
