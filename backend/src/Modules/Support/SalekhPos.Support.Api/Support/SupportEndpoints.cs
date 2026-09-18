using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Support.Application;
using SalekhPos.Support.Contracts;

namespace SalekhPos.Support.Api.Support;

public static class SupportEndpoints
{
    public static void MapSupportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/support/tickets")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("", List);
        group.MapPost("", Create);
        group.MapGet("/{ticketId:guid}", Get);
        group.MapPost("/{ticketId:guid}/transitions", Transition);
        group.MapPost("/{ticketId:guid}/diagnostics", AddDiagnostic);
    }

    private static SupportIdentity Identity(HttpContext context) => new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static async Task<IResult> List(Guid organizationId, HttpContext context, ISupportService service, CancellationToken cancellationToken)
    {
        var size = 50; Guid? after = null;
        if (context.Request.Query.TryGetValue("pageSize", out var s) && (!int.TryParse(s, out size) || size is < 1 or > 100)) return Invalid();
        if (context.Request.Query.TryGetValue("after", out var a)) { if (!Guid.TryParse(a, out var parsed) || parsed == Guid.Empty) return Invalid(); after = parsed; }
        try { return Results.Ok(await service.ListTicketsAsync(Identity(context), organizationId, size, after, context.Request.Query["status"].FirstOrDefault(), cancellationToken)); }
        catch (ArgumentException) { return Invalid(); }
    }
    private static async Task<IResult> Get(Guid organizationId, Guid ticketId, HttpContext context, ISupportService service, CancellationToken cancellationToken)
    { try { return Results.Ok(await service.GetTicketAsync(Identity(context), organizationId, ticketId, cancellationToken)); } catch (ArgumentException) { return Invalid(); } }
    private static async Task<IResult> Create(Guid organizationId, CreateSupportTicketRequest request, HttpContext context,
        ISupportService service, IAntiforgery antiforgery, CancellationToken cancellationToken)
    {
        var operation = await Operation(context, antiforgery); if (operation is null) return Invalid();
        try
        {
            var result = await service.CreateTicketAsync(Identity(context), new(organizationId, Guid.NewGuid(), operation.Value,
                request.BranchId, request.Subject, request.Description, request.Priority), cancellationToken);
            return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/support/tickets/{result.Value.Id:D}", result.Value) : Results.Ok(result.Value);
        }
        catch (ArgumentException) { return Invalid(); }
    }
    private static async Task<IResult> Transition(Guid organizationId, Guid ticketId, TransitionSupportTicketRequest request,
        HttpContext context, ISupportService service, IAntiforgery antiforgery, CancellationToken cancellationToken)
    { var operation = await Operation(context, antiforgery); if (operation is null) return Invalid(); try { return Results.Ok(await service.TransitionTicketAsync(Identity(context), organizationId, ticketId, operation.Value, request, cancellationToken)); } catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return Invalid(); } }
    private static async Task<IResult> AddDiagnostic(Guid organizationId, Guid ticketId, AddDiagnosticReferenceRequest request,
        HttpContext context, ISupportService service, IAntiforgery antiforgery, CancellationToken cancellationToken)
    {
        var operation = await Operation(context, antiforgery); if (operation is null) return Invalid();
        try
        {
            var result = await service.AddDiagnosticAsync(Identity(context), new(organizationId, ticketId, Guid.NewGuid(), operation.Value,
                request.Kind, request.Reference, request.Sha256), cancellationToken);
            return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/support/tickets/{ticketId:D}/diagnostics/{result.Value.Id:D}", result.Value) : Results.Ok(result.Value);
        }
        catch (ArgumentException) { return Invalid(); }
    }
    private static async Task<Guid?> Operation(HttpContext context, IAntiforgery antiforgery)
    { if (!await Mutation(context, antiforgery) || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operation) || operation == Guid.Empty) return null; return operation; }
    private static async Task<bool> Mutation(HttpContext context, IAntiforgery antiforgery)
    { if (context.Request.Headers.ContainsKey("Authorization")) return true; try { await antiforgery.ValidateRequestAsync(context); return true; } catch (AntiforgeryValidationException) { return false; } }
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The support request is invalid", extensions: new Dictionary<string, object?> { ["code"] = "invalid_support_request" });
}
