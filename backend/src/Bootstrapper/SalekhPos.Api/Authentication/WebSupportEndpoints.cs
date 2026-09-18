using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using SalekhPos.Support.Application;
using SalekhPos.Support.Contracts;

namespace SalekhPos.Api.Authentication;

public static class WebSupportEndpoints
{
    public static void MapWebSupportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff/api/v1")
            .AllowAnonymous()
            .RequireRateLimiting("business");

        group.MapGet("/organizations/{organizationId:guid}/support/tickets", List);
        group.MapPost("/organizations/{organizationId:guid}/support/tickets", Create);
        group.MapGet("/organizations/{organizationId:guid}/support/tickets/{ticketId:guid}", Get);
        group.MapPost("/organizations/{organizationId:guid}/support/tickets/{ticketId:guid}/transitions", Transition);
        group.MapPost("/organizations/{organizationId:guid}/support/tickets/{ticketId:guid}/diagnostics", AddDiagnostic);
    }

    private static async Task<IResult> List(Guid organizationId, HttpContext context,
        WebAuthenticationState state, ISupportService support, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!TryListQuery(context, out var pageSize, out var after, out var status))
            return Invalid("invalid_support_query");
        try
        {
            return Results.Ok(await support.ListTicketsAsync(identity, organizationId, pageSize, after, status, cancellationToken));
        }
        catch (ArgumentException) { return Invalid("invalid_support_query"); }
    }

    private static async Task<IResult> Get(Guid organizationId, Guid ticketId, HttpContext context,
        WebAuthenticationState state, ISupportService support, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (ticketId == Guid.Empty) return Invalid("invalid_support_query");
        return Results.Ok(await support.GetTicketAsync(identity, organizationId, ticketId, cancellationToken));
    }

    private static async Task<IResult> Create(Guid organizationId, CreateSupportTicketRequest request,
        HttpContext context, WebAuthenticationState state, IAntiforgery antiforgery,
        ISupportService support, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!await ValidMutation(context, state, antiforgery) || !TryOperationId(context, out var operationId))
            return Invalid("invalid_support_request");
        try
        {
            var result = await support.CreateTicketAsync(identity,
                new(organizationId, Guid.NewGuid(), operationId, request.BranchId,
                    request.Subject, request.Description, request.Priority), cancellationToken);
            return result.Created
                ? Results.Created($"/bff/api/v1/organizations/{organizationId:D}/support/tickets/{result.Value.Id:D}", result.Value)
                : Results.Ok(result.Value);
        }
        catch (ArgumentException) { return Invalid("invalid_support_request"); }
    }

    private static async Task<IResult> Transition(Guid organizationId, Guid ticketId,
        TransitionSupportTicketRequest request, HttpContext context, WebAuthenticationState state,
        IAntiforgery antiforgery, ISupportService support, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (ticketId == Guid.Empty || !await ValidMutation(context, state, antiforgery)
            || !TryOperationId(context, out var operationId))
            return Invalid("invalid_support_request");
        try
        {
            return Results.Ok(await support.TransitionTicketAsync(
                identity, organizationId, ticketId, operationId, request, cancellationToken));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Invalid("invalid_support_request");
        }
    }

    private static async Task<IResult> AddDiagnostic(Guid organizationId, Guid ticketId,
        AddDiagnosticReferenceRequest request, HttpContext context, WebAuthenticationState state,
        IAntiforgery antiforgery, ISupportService support, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (ticketId == Guid.Empty || !await ValidMutation(context, state, antiforgery)
            || !TryOperationId(context, out var operationId))
            return Invalid("invalid_support_request");
        try
        {
            var result = await support.AddDiagnosticAsync(identity,
                new(organizationId, ticketId, Guid.NewGuid(), operationId,
                    request.Kind, request.Reference, request.Sha256), cancellationToken);
            return result.Created
                ? Results.Created($"/bff/api/v1/organizations/{organizationId:D}/support/tickets/{ticketId:D}/diagnostics/{result.Value.Id:D}", result.Value)
                : Results.Ok(result.Value);
        }
        catch (ArgumentException) { return Invalid("invalid_support_request"); }
    }

    private static async Task<SupportIdentity?> Identity(HttpContext context, WebAuthenticationState state)
    {
        if (state.Settings is null) return null;
        var result = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        if (!result.Succeeded || result.Principal is null) return null;
        var issuers = result.Principal.FindAll("iss").ToArray();
        var subjects = result.Principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1) return null;
        try
        {
            var identity = new SupportIdentity(issuers[0].Value, subjects[0].Value);
            identity.Validate();
            return identity;
        }
        catch (ArgumentException) { return null; }
    }

    private static bool TryListQuery(HttpContext context, out int pageSize, out Guid? after, out string? status)
    {
        pageSize = 50;
        after = null;
        status = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "after" and not "status"))
            return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], out pageSize) || pageSize is < 1 or > 100))
            return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor)
                || cursor == Guid.Empty) return false;
            after = cursor;
        }
        if (context.Request.Query.TryGetValue("status", out var statuses))
        {
            if (statuses.Count != 1 || string.IsNullOrWhiteSpace(statuses[0])
                || statuses[0]!.Length > 32 || statuses[0]!.Any(char.IsControl)) return false;
            status = statuses[0]!.Trim();
        }
        return true;
    }

    private static Task<bool> ValidMutation(HttpContext context, WebAuthenticationState state,
        IAntiforgery antiforgery) => state.Settings is null
        ? Task.FromResult(false)
        : WebAuthentication.ValidateMutation(context, antiforgery, state.Settings);

    private static bool TryOperationId(HttpContext context, out Guid operationId) =>
        Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out operationId)
        && operationId != Guid.Empty;

    private static IResult Unauthenticated(WebAuthenticationState state) => state.Settings is null
        ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = "web_authentication_unavailable" })
        : Results.Unauthorized();

    private static IResult Invalid(string code) => Results.Problem(statusCode: 400,
        title: "The support request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
