using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Sync.Application.SyncMessages;
using SalekhPos.Sync.Contracts.SyncMessages;
namespace SalekhPos.Sync.Api.SyncMessages;

public static class SyncEndpoints { public static void MapSyncEndpoints(this WebApplication app) { var g = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/devices/{deviceId:guid}/sync").RequireAuthorization().RequireRateLimiting("business"); g.MapPost("/messages", async (Guid organizationId, Guid branchId, Guid deviceId, IngestSyncMessageRequest request, HttpContext c, ISyncIngestion s, CancellationToken ct) => { try { return Results.Ok(await s.IngestAsync(Id(c), new(organizationId, branchId, deviceId, request.MessageId, request.Sequence, request.ProtocolVersion, request.MessageType, request.Payload), ct)); } catch (ArgumentException) { return Invalid(); } }); g.MapGet("/checkpoint", async (Guid organizationId, Guid branchId, Guid deviceId, HttpContext c, ISyncIngestion s, CancellationToken ct) => { var x = await s.ReadCheckpointAsync(Id(c), organizationId, branchId, deviceId, ct); return x is null ? Results.NotFound() : Results.Ok(x); }); } private static SyncIdentity Id(HttpContext c) => new(c.User.FindFirst("iss")!.Value, c.User.FindFirst("sub")!.Value); private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The sync message is invalid", extensions: new Dictionary<string, object?> { { "code", "invalid_sync_message" } }); }
