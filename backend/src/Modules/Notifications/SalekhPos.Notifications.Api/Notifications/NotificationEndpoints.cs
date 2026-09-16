using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Notifications.Application.Notifications;
using SalekhPos.Notifications.Contracts.Notifications;

namespace SalekhPos.Notifications.Api.Notifications;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/notifications")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("", List);
        group.MapPost("", Create);
        group.MapPost("/{notificationId:guid}/read", MarkRead);
        group.MapGet("/preferences", ReadPreferences);
        group.MapPut("/preferences", UpdatePreferences);
    }

    private static NotificationIdentity Identity(HttpContext c) => new(c.User.FindFirst("iss")!.Value, c.User.FindFirst("sub")!.Value);
    private static async Task<IResult> List(Guid organizationId, HttpContext c, INotificationCenter center, CancellationToken ct)
    {
        var pageSize = 50; Guid? after = null; var unreadOnly = false;
        if (c.Request.Query.TryGetValue("pageSize", out var p) && (!int.TryParse(p, out pageSize) || pageSize is < 1 or > 100)) return Invalid();
        if (c.Request.Query.TryGetValue("after", out var a)) { if (!Guid.TryParse(a, out var parsed) || parsed == Guid.Empty) return Invalid(); after = parsed; }
        if (c.Request.Query.TryGetValue("unreadOnly", out var u) && !bool.TryParse(u, out unreadOnly)) return Invalid();
        return Results.Ok(await center.ListMineAsync(Identity(c), organizationId, pageSize, after, unreadOnly, ct));
    }

    private static async Task<IResult> Create(Guid organizationId, CreateNotificationRequest request, HttpContext c,
        INotificationCenter center, IAntiforgery antiforgery, CancellationToken ct)
    {
        if (!await Mutation(c, antiforgery) || !Guid.TryParseExact(c.Request.Headers["Idempotency-Key"], "D", out var op) || op == Guid.Empty) return Invalid();
        try
        {
            var result = await center.CreateAsync(Identity(c), new(organizationId, Guid.NewGuid(), op, request.BranchId,
                request.RecipientSubject, request.Title, request.Body, request.Severity), ct);
            return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/notifications/{result.Notification.Id:D}", result.Notification) : Results.Ok(result.Notification);
        }
        catch (ArgumentException) { return Invalid(); }
    }
    private static async Task<IResult> MarkRead(Guid organizationId, Guid notificationId, HttpContext c,
        INotificationCenter center, IAntiforgery antiforgery, CancellationToken ct)
    {
        if (!await Mutation(c, antiforgery)) return Invalid();
        try { return Results.Ok(await center.MarkReadAsync(Identity(c), organizationId, notificationId, ct)); }
        catch (ArgumentException) { return Invalid(); }
    }

    private static async Task<IResult> ReadPreferences(Guid organizationId, HttpContext c, INotificationCenter center, CancellationToken ct)
        => Results.Ok(await center.ReadPreferencesAsync(Identity(c), organizationId, ct));

    private static async Task<IResult> UpdatePreferences(Guid organizationId, UpdateNotificationPreferencesRequest request, HttpContext c,
        INotificationCenter center, IAntiforgery antiforgery, CancellationToken ct)
    {
        if (!await Mutation(c, antiforgery)) return Invalid();
        return Results.Ok(await center.UpdatePreferencesAsync(Identity(c), organizationId, request, ct));
    }

    private static async Task<bool> Mutation(HttpContext c, IAntiforgery antiforgery)
    {
        if (c.Request.Headers.ContainsKey("Authorization")) return true;
        try { await antiforgery.ValidateRequestAsync(c); return true; } catch (AntiforgeryValidationException) { return false; }
    }
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The notification request is invalid",
        extensions: new Dictionary<string, object?> { { "code", "invalid_notification_request" } });
}
