using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using SalekhPos.ShiftManagement.Application.Shifts;
using SalekhPos.ShiftManagement.Contracts.Shifts;

namespace SalekhPos.ShiftManagement.Api.Shifts;

public static class ShiftEndpoints
{
    public static void MapShiftEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/shifts").RequireAuthorization().RequireRateLimiting("business");
        group.MapPost("/open", async (Guid organizationId, Guid branchId, OpenShiftRequest request, HttpContext context,
            IShiftDeviceRequestAuthorizer proof, IShiftService service, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId) || operationId == Guid.Empty) return Invalid();
            var deviceIdText = Header(context, "X-SalekhPos-Device-Id");
            if (deviceIdText is null || !Guid.TryParseExact(deviceIdText, "D", out var deviceId) || deviceId == Guid.Empty
                || !string.Equals(deviceIdText, deviceId.ToString("D"), StringComparison.Ordinal))
                throw new ShiftRequestAuthenticationException();
            if (!context.Items.TryGetValue(ShiftRequestBodyDigestMiddleware.DigestItemKey, out var digestValue)
                || digestValue is not string digest)
                throw new ShiftRequestAuthenticationException();
            var identity = Identity(context);
            await proof.VerifyAsync(new(identity, organizationId, branchId, deviceId, request.RegisterId,
                "POST", OpenPath(organizationId, branchId), $"shift-open:{operationId:D}", digest,
                new(Header(context, "X-SalekhPos-Device-Credential"), Header(context, "X-SalekhPos-Device-Timestamp"),
                    Header(context, "X-SalekhPos-Device-Nonce"), Header(context, "X-SalekhPos-Device-Signature"))),
                cancellationToken);
            try
            {
                var result = await service.OpenAsync(identity, new(organizationId, branchId, Guid.NewGuid(), operationId, request.RegisterId, request.Currency, request.OpeningBalance), cancellationToken);
                return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/open?registerId={result.Shift.RegisterId:D}", result.Shift) : Results.Ok(result.Shift);
            }
            catch (ArgumentException) { return Invalid(); }
        });
        group.MapGet("/open", async (Guid organizationId, Guid branchId, Guid registerId, HttpContext context, IShiftService service, CancellationToken cancellationToken) =>
        {
            var shift = await service.ReadOpenAsync(Identity(context), organizationId, branchId, registerId, cancellationToken);
            return shift is null ? Results.NotFound() : Results.Ok(shift);
        });
        group.MapPost("/{shiftId:guid}/cash-movements", async (Guid organizationId, Guid branchId, Guid shiftId, RecordCashMovementRequest request, HttpContext context, IShiftService service, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId) || operationId == Guid.Empty) return Invalid();
            try { var result = await service.RecordCashMovementAsync(Identity(context), new(organizationId, branchId, shiftId, Guid.NewGuid(), operationId, request.Kind, request.Amount, request.Reason), cancellationToken); return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/{shiftId:D}/cash-movements/{result.Movement.Id:D}", result.Movement) : Results.Ok(result.Movement); }
            catch (ArgumentException) { return Invalid(); }
        });
        group.MapGet("/{shiftId:guid}/cash-movements", async (Guid organizationId, Guid branchId, Guid shiftId, HttpContext context, IShiftService service, CancellationToken cancellationToken) => Results.Ok(await service.ListCashMovementsAsync(Identity(context), organizationId, branchId, shiftId, cancellationToken)));
        group.MapPost("/{shiftId:guid}/close", async (Guid organizationId, Guid branchId, Guid shiftId, CloseShiftRequest request, HttpContext context, IShiftService service, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId) || operationId == Guid.Empty) return Invalid();
            try { var result = await service.CloseAsync(Identity(context), new(organizationId, branchId, shiftId, operationId, request.CountedCash), cancellationToken); return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/{shiftId:D}", result.Shift) : Results.Ok(result.Shift); }
            catch (ArgumentException) { return Invalid(); }
        });
        group.MapGet("/closed", async (Guid organizationId, Guid branchId, HttpContext context, IShiftService service, CancellationToken cancellationToken) =>
        {
            if (!TryListQuery(context, out var pageSize, out var after)) return InvalidQuery();
            try { return Results.Ok(await service.ListClosedAsync(Identity(context), organizationId, branchId, pageSize, after, cancellationToken)); }
            catch (ArgumentException) { return InvalidQuery(); }
        });
        group.MapGet("/{shiftId:guid}", async (Guid organizationId, Guid branchId, Guid shiftId, HttpContext context, IShiftService service, CancellationToken cancellationToken) =>
        {
            var shift = await service.ReadClosedAsync(Identity(context), organizationId, branchId, shiftId, cancellationToken);
            return shift is null ? Results.NotFound() : Results.Ok(shift);
        });
    }
    private static string? Header(HttpContext context, string name)
    {
        StringValues values = context.Request.Headers[name];
        return values.Count switch { 0 => null, 1 => values[0], _ => throw new ShiftRequestAuthenticationException() };
    }
    private static string OpenPath(Guid organizationId, Guid branchId) =>
        $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/open";
    private static ShiftIdentity Identity(HttpContext context) => new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The shift request is invalid", extensions: new Dictionary<string, object?> { ["code"] = "invalid_shift_request" });
    private static IResult InvalidQuery() => Results.Problem(statusCode: 400, title: "The shift query is invalid", extensions: new Dictionary<string, object?> { ["code"] = "invalid_shift_query" });
    private static bool TryListQuery(HttpContext context, out int pageSize, out Guid? after)
    {
        pageSize = 50; after = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "after")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture, out pageSize)
                || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor) || cursor == Guid.Empty) return false;
            after = cursor;
        }
        return true;
    }
}
