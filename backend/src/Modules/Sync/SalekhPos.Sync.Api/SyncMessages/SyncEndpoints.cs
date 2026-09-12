using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using SalekhPos.Sync.Application.SyncMessages;
using SalekhPos.Sync.Contracts.SyncMessages;
namespace SalekhPos.Sync.Api.SyncMessages;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/devices/{deviceId:guid}/sync").RequireAuthorization().RequireRateLimiting("business");
        g.MapPost("/messages", async (Guid organizationId, Guid branchId, Guid deviceId, IngestSyncMessageRequest request,
            HttpContext context, ISyncDeviceRequestAuthorizer proof, ISyncIngestion ingestion, CancellationToken ct) =>
        {
            try
            {
                await proof.VerifyAsync(Proof(context, organizationId, branchId, deviceId,
                    $"message:{request.MessageId:D}", MessagesPath(organizationId, branchId, deviceId)), ct);
                return Results.Ok(await ingestion.IngestAsync(Id(context), new(organizationId, branchId, deviceId,
                    request.MessageId, request.Sequence, request.ProtocolVersion, request.MessageType, request.Payload), ct));
            }
            catch (ArgumentException) { return Invalid(); }
        });
        g.MapGet("/messages/{messageId:guid}", async (Guid organizationId, Guid branchId, Guid deviceId, Guid messageId,
            HttpContext context, ISyncDeviceRequestAuthorizer proof, ISyncIngestion ingestion, CancellationToken ct) =>
        {
            await proof.VerifyAsync(Proof(context, organizationId, branchId, deviceId, $"message:{messageId:D}",
                $"{MessagesPath(organizationId, branchId, deviceId)}/{messageId:D}"), ct);
            var result = await ingestion.ReadMessageAsync(Id(context), organizationId, branchId, deviceId, messageId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        g.MapGet("/messages", async (Guid organizationId, Guid branchId, Guid deviceId, HttpContext context,
            ISyncDeviceRequestAuthorizer proof, ISyncIngestion ingestion, CancellationToken ct) =>
        {
            try
            {
                if (context.Request.Query.Keys.Any(k => k is not "pageSize" and not "afterSequence")) return Invalid();
                var size = 50;
                if (context.Request.Query.TryGetValue("pageSize", out var pageSize)
                    && (!int.TryParse(pageSize, out size) || size is < 1 or > 100)) return Invalid();
                long? after = null;
                if (context.Request.Query.TryGetValue("afterSequence", out var afterSequence))
                {
                    if (!long.TryParse(afterSequence, out var parsed) || parsed <= 0) return Invalid();
                    after = parsed;
                }
                await proof.VerifyAsync(Proof(context, organizationId, branchId, deviceId,
                    $"history:{size}:{after?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}",
                    MessagesPath(organizationId, branchId, deviceId)), ct);
                return Results.Ok(await ingestion.ReadHistoryAsync(Id(context), organizationId, branchId, deviceId, size, after, ct));
            }
            catch (ArgumentException) { return Invalid(); }
        });
        g.MapGet("/checkpoint", async (Guid organizationId, Guid branchId, Guid deviceId, HttpContext context,
            ISyncDeviceRequestAuthorizer proof, ISyncIngestion ingestion, CancellationToken ct) =>
        {
            await proof.VerifyAsync(Proof(context, organizationId, branchId, deviceId, "checkpoint",
                $"{BasePath(organizationId, branchId, deviceId)}/checkpoint"), ct);
            var result = await ingestion.ReadCheckpointAsync(Id(context), organizationId, branchId, deviceId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
    }

    private static SyncDeviceRequestProofContext Proof(HttpContext context, Guid organizationId, Guid branchId, Guid deviceId,
        string operationIdentity, string canonicalPath)
    {
        if (!context.Items.TryGetValue(SyncRequestBodyDigestMiddleware.DigestItemKey, out var value) || value is not string digest)
            throw new SyncRequestAuthenticationException();
        return new(new(Id(context).Issuer, Id(context).Subject), organizationId, branchId, deviceId,
            context.Request.Method.ToUpperInvariant(), canonicalPath, operationIdentity, digest,
            new SyncDeviceRequestProofHeaders(Header(context, "X-SalekhPos-Device-Credential"), Header(context, "X-SalekhPos-Device-Timestamp"),
                Header(context, "X-SalekhPos-Device-Nonce"), Header(context, "X-SalekhPos-Device-Signature")));
    }

    private static string? Header(HttpContext context, string name)
    {
        StringValues values = context.Request.Headers[name];
        return values.Count switch { 0 => null, 1 => values[0], _ => throw new SyncRequestAuthenticationException() };
    }

    private static string BasePath(Guid organizationId, Guid branchId, Guid deviceId) =>
        $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/devices/{deviceId:D}/sync";
    private static string MessagesPath(Guid organizationId, Guid branchId, Guid deviceId) =>
        $"{BasePath(organizationId, branchId, deviceId)}/messages";
    private static SyncIdentity Id(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The sync message is invalid",
        extensions: new Dictionary<string, object?> { { "code", "invalid_sync_message" } });
}
