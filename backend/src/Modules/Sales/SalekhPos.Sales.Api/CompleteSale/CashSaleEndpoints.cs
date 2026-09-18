using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using SalekhPos.Sales.Application.CompleteSale;
using SalekhPos.Sales.Contracts.CompleteSale;

namespace SalekhPos.Sales.Api.CompleteSale;

public static class CashSaleEndpoints
{
    public static void MapCashSaleEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales",
            async (Guid organizationId, Guid branchId, HttpContext context, ISaleReader reader,
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

        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/{saleId:guid}",
            async (Guid organizationId, Guid branchId, Guid saleId, HttpContext context,
                ISaleReader reader, CancellationToken cancellationToken) =>
            {
                try
                {
                    var sale = await reader.ReadAsync(Identity(context), organizationId, branchId, saleId,
                        cancellationToken);
                    return sale is null ? Results.NotFound() : Results.Ok(sale);
                }
                catch (ArgumentException) { return InvalidQuery(); }
            }).RequireAuthorization().RequireRateLimiting("business");

        app.MapPost("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/cash",
            async (Guid organizationId, Guid branchId, CompleteCashSaleRequest request, HttpContext context,
                ISalesDeviceRequestAuthorizer proof, ICashSaleCompletion completion,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                    || operationId == Guid.Empty || request.Lines is null) return Invalid();
                var identity = Identity(context);
                var deviceId = DeviceId(context);
                if (!context.Items.TryGetValue(SalesRequestBodyDigestMiddleware.DigestItemKey, out var digestValue)
                    || digestValue is not string digest)
                    throw new SalesRequestAuthenticationException();
                await proof.VerifyAsync(new(identity, organizationId, branchId, deviceId, "POST",
                    CashPath(organizationId, branchId), $"cash-sale:{operationId:D}", digest,
                    new(Header(context, "X-SalekhPos-Device-Credential"),
                        Header(context, "X-SalekhPos-Device-Timestamp"),
                        Header(context, "X-SalekhPos-Device-Nonce"),
                        Header(context, "X-SalekhPos-Device-Signature"))), cancellationToken);
                try
                {
                    var command = new CompleteCashSaleCommand(organizationId, branchId, deviceId, request.ShiftId,
                        Guid.NewGuid(), operationId, request.Lines, request.CashReceived, request.SuspendedCartId);
                    var result = await completion.CompleteAsync(identity, command, cancellationToken);
                    return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/sales/{result.Sale.Id:D}", result.Sale)
                        : Results.Ok(result.Sale);
                }
                catch (ArgumentException) { return Invalid(); }
                catch (OverflowException) { return Invalid(); }
            }).RequireAuthorization().RequireRateLimiting("business");
    }

    private static Guid DeviceId(HttpContext context)
    {
        var value = Header(context, "X-SalekhPos-Device-Id");
        if (value is null || !Guid.TryParseExact(value, "D", out var deviceId) || deviceId == Guid.Empty
            || !string.Equals(value, deviceId.ToString("D"), StringComparison.Ordinal))
            throw new SalesRequestAuthenticationException();
        return deviceId;
    }
    private static string? Header(HttpContext context, string name)
    {
        StringValues values = context.Request.Headers[name];
        return values.Count switch
        {
            0 => null,
            1 => values[0],
            _ => throw new SalesRequestAuthenticationException()
        };
    }
    private static string CashPath(Guid organizationId, Guid branchId) =>
        $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/sales/cash";
    private static SalesIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The cash sale request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_cash_sale_request" });
    private static IResult InvalidQuery() => Results.Problem(statusCode: 400, title: "The sale query is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_sale_query" });

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
