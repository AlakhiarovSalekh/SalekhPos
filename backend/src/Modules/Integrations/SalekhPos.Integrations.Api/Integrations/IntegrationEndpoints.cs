using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Integrations.Application;
using SalekhPos.Integrations.Contracts;

namespace SalekhPos.Integrations.Api.Integrations;

public static class IntegrationEndpoints
{
    public static void MapIntegrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/integrations")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("/connections", ListConnections);
        group.MapPost("/connections", CreateConnection);
        group.MapPost("/connections/{connectionId:guid}/disable", DisableConnection);
        group.MapGet("/webhooks", ListWebhooks);
        group.MapPost("/webhooks", EnqueueWebhook);
        group.MapPost("/webhooks/lease", LeaseWebhook);
        group.MapPost("/webhooks/{deliveryId:guid}/attempts", RecordAttempt);
    }

    private static IntegrationIdentity Identity(HttpContext context) => new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static async Task<IResult> ListConnections(Guid organizationId, HttpContext context, IIntegrationService service, CancellationToken cancellationToken)
    { var page = Page(context); if (page is null) return Invalid(); return Results.Ok(await service.ListConnectionsAsync(Identity(context), organizationId, page.Value.Size, page.Value.After, cancellationToken)); }
    private static async Task<IResult> ListWebhooks(Guid organizationId, HttpContext context, IIntegrationService service, CancellationToken cancellationToken)
    { var page = Page(context); if (page is null) return Invalid(); return Results.Ok(await service.ListDeliveriesAsync(Identity(context), organizationId, page.Value.Size, page.Value.After, cancellationToken)); }

    private static async Task<IResult> CreateConnection(Guid organizationId, CreateIntegrationConnectionRequest request,
        HttpContext context, IIntegrationService service, IAntiforgery antiforgery, CancellationToken cancellationToken)
    {
        var operation = await Operation(context, antiforgery); if (operation is null) return Invalid();
        try
        {
            var result = await service.CreateConnectionAsync(Identity(context), new(organizationId, Guid.NewGuid(), operation.Value,
                request.Provider, request.DisplayName, request.Endpoint, request.SecretReference), cancellationToken);
            return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/integrations/connections/{result.Value.Id:D}", result.Value) : Results.Ok(result.Value);
        }
        catch (ArgumentException) { return Invalid(); }
    }

    private static async Task<IResult> DisableConnection(Guid organizationId, Guid connectionId, DisableIntegrationConnectionRequest request,
        HttpContext context, IIntegrationService service, IAntiforgery antiforgery, CancellationToken cancellationToken)
    { var operation = await Operation(context, antiforgery); if (operation is null) return Invalid(); try { return Results.Ok(await service.DisableConnectionAsync(Identity(context), organizationId, connectionId, operation.Value, request.Reason, cancellationToken)); } catch (ArgumentException) { return Invalid(); } }

    private static async Task<IResult> EnqueueWebhook(Guid organizationId, EnqueueWebhookRequest request,
        HttpContext context, IIntegrationService service, IAntiforgery antiforgery, CancellationToken cancellationToken)
    {
        var operation = await Operation(context, antiforgery); if (operation is null) return Invalid();
        try
        {
            var result = await service.EnqueueWebhookAsync(Identity(context), new(organizationId, Guid.NewGuid(), operation.Value,
                request.ConnectionId, request.EventId, request.EventType, request.PayloadSha256, request.PayloadReference), cancellationToken);
            return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/integrations/webhooks/{result.Value.Id:D}", result.Value) : Results.Ok(result.Value);
        }
        catch (ArgumentException) { return Invalid(); }
    }

    private static async Task<IResult> RecordAttempt(Guid organizationId, Guid deliveryId, RecordWebhookAttemptRequest request,
        HttpContext context, IIntegrationService service, IAntiforgery antiforgery, CancellationToken cancellationToken)
    { if (!await Mutation(context, antiforgery)) return Invalid(); try { return Results.Ok(await service.RecordAttemptAsync(Identity(context), organizationId, deliveryId, request, cancellationToken)); } catch (ArgumentException) { return Invalid(); } }

    private static async Task<IResult> LeaseWebhook(Guid organizationId, LeaseWebhookRequest request,
        HttpContext context, IIntegrationService service, IAntiforgery antiforgery, CancellationToken cancellationToken)
    { if (!await Mutation(context, antiforgery)) return Invalid(); try { var result = await service.LeaseNextWebhookAsync(Identity(context), organizationId, request, cancellationToken); return result is null ? Results.NoContent() : Results.Ok(result); } catch (ArgumentException) { return Invalid(); } }

    private static (int Size, Guid? After)? Page(HttpContext context)
    {
        var size = 50; Guid? after = null;
        if (context.Request.Query.TryGetValue("pageSize", out var rawSize) && (!int.TryParse(rawSize, out size) || size is < 1 or > 100)) return null;
        if (context.Request.Query.TryGetValue("after", out var rawAfter)) { if (!Guid.TryParse(rawAfter, out var parsed) || parsed == Guid.Empty) return null; after = parsed; }
        return (size, after);
    }
    private static async Task<Guid?> Operation(HttpContext context, IAntiforgery antiforgery)
    { if (!await Mutation(context, antiforgery) || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operation) || operation == Guid.Empty) return null; return operation; }
    private static async Task<bool> Mutation(HttpContext context, IAntiforgery antiforgery)
    { if (context.Request.Headers.ContainsKey("Authorization")) return true; try { await antiforgery.ValidateRequestAsync(context); return true; } catch (AntiforgeryValidationException) { return false; } }
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The integration request is invalid", extensions: new Dictionary<string, object?> { ["code"] = "invalid_integration_request" });
}
