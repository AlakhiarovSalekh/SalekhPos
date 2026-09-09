using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.ShiftManagement.Application.Shifts;
using SalekhPos.ShiftManagement.Contracts.Shifts;

namespace SalekhPos.ShiftManagement.Api.Shifts;

public static class ShiftEndpoints
{
    public static void MapShiftEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/shifts").RequireAuthorization().RequireRateLimiting("business");
        group.MapPost("/open", async (Guid organizationId, Guid branchId, OpenShiftRequest request, HttpContext context, IShiftService service, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId) || operationId == Guid.Empty) return Invalid();
            try
            {
                var result = await service.OpenAsync(Identity(context), new(organizationId, branchId, Guid.NewGuid(), operationId, request.RegisterId, request.Currency, request.OpeningBalance), cancellationToken);
                return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/open?registerId={result.Shift.RegisterId:D}", result.Shift) : Results.Ok(result.Shift);
            }
            catch (ArgumentException) { return Invalid(); }
        });
        group.MapGet("/open", async (Guid organizationId, Guid branchId, Guid registerId, HttpContext context, IShiftService service, CancellationToken cancellationToken) =>
        {
            var shift = await service.ReadOpenAsync(Identity(context), organizationId, branchId, registerId, cancellationToken);
            return shift is null ? Results.NotFound() : Results.Ok(shift);
        });
    }
    private static ShiftIdentity Identity(HttpContext context) => new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The shift request is invalid", extensions: new Dictionary<string, object?> { ["code"] = "invalid_shift_request" });
}
