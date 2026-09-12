using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Devices.Application.Devices;
using SalekhPos.Devices.Contracts.Devices;
namespace SalekhPos.Devices.Api.Devices;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/devices").RequireAuthorization().RequireRateLimiting("business");
        g.MapPost("", async (Guid organizationId, Guid branchId, RegisterDeviceRequest request, HttpContext context, IDeviceRegistry registry, CancellationToken ct) => { if (!Operation(context, out var op)) return Invalid(); try { var result = await registry.RegisterAsync(Id(context), new(organizationId, branchId, Guid.NewGuid(), op, request.RegisterId, request.Code, request.Name, request.Platform, request.SyncProtocolVersion, request.PublicKey), ct); return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/devices/{result.Device.Id:D}", result.Device) : Results.Ok(result.Device); } catch (ArgumentException) { return Invalid(); } });
        g.MapPost("/{deviceId:guid}/trust", async (Guid organizationId, Guid branchId, Guid deviceId, TrustDeviceRequest request, HttpContext context, IDeviceRegistry registry, CancellationToken ct) => { if (!Operation(context, out var op)) return Invalid(); try { return Results.Ok(await registry.TrustAsync(Id(context), new(organizationId, branchId, deviceId, op, request.CredentialId, request.ProofChallenge, request.Signature), ct)); } catch (ArgumentException) { return Invalid(); } });
        g.MapPost("/{deviceId:guid}/revoke", async (Guid organizationId, Guid branchId, Guid deviceId, RevokeDeviceRequest request, HttpContext context, IDeviceRegistry registry, CancellationToken ct) => { if (!Operation(context, out var op)) return Invalid(); try { return Results.Ok(await registry.RevokeAsync(Id(context), new(organizationId, branchId, deviceId, op, request.Reason), ct)); } catch (ArgumentException) { return Invalid(); } });
        g.MapGet("/{deviceId:guid}", async (Guid organizationId, Guid branchId, Guid deviceId, HttpContext context, IDeviceRegistry registry, CancellationToken ct) => { var result = await registry.ReadAsync(Id(context), organizationId, branchId, deviceId, ct); return result is null ? Results.NotFound() : Results.Ok(result); });
        g.MapGet("", async (Guid organizationId, Guid branchId, HttpContext context, IDeviceRegistry registry, CancellationToken ct) => { if (context.Request.Query.Keys.Any(k => k is not "pageSize" and not "after")) return Invalid(); var size = 50; if (context.Request.Query.TryGetValue("pageSize", out var s) && (!int.TryParse(s, out size) || size is < 1 or > 100)) return Invalid(); Guid? after = null; if (context.Request.Query.TryGetValue("after", out var a)) { if (!Guid.TryParseExact(a, "D", out var cursor) || cursor == Guid.Empty) return Invalid(); after = cursor; } return Results.Ok(await registry.ListAsync(Id(context), organizationId, branchId, size, after, ct)); });
    }
    private static DeviceIdentity Id(HttpContext c) => new(c.User.FindFirst("iss")!.Value, c.User.FindFirst("sub")!.Value);
    private static bool Operation(HttpContext c, out Guid operationId) => Guid.TryParseExact(c.Request.Headers["Idempotency-Key"], "D", out operationId) && operationId != Guid.Empty;
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The device request is invalid", extensions: new Dictionary<string, object?> { { "code", "invalid_device_request" } });
}
