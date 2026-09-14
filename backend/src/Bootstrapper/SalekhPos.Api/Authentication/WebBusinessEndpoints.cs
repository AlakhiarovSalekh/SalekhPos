using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using SalekhPos.Authorization.Application;
using SalekhPos.Authorization.Infrastructure;
using SalekhPos.Payments.Application.Payments;
using SalekhPos.Returns.Application.Returns;
using SalekhPos.Returns.Contracts.Returns;
using SalekhPos.Sales.Application.CompleteSale;
using SalekhPos.Sales.Application.Voids;

namespace SalekhPos.Api.Authentication;

public static class WebBusinessEndpoints
{
    public static void MapWebBusinessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff/api/v1").AllowAnonymous().RequireRateLimiting("business");

        group.MapGet("/organizations/{organizationId:guid}/branches",
            async (Guid organizationId, HttpContext context, WebAuthenticationState state,
                BranchAccessReader reader, CancellationToken cancellationToken) =>
            {
                var identity = await AccessIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId) || !TryListQuery(context, out var pageSize, out var after)) return InvalidQuery();
                return Results.Ok(await reader.ReadAsync(identity, organizationId, pageSize, after, null,
                    cancellationToken));
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/sales",
            async (Guid organizationId, Guid branchId, HttpContext context, WebAuthenticationState state,
                ISaleReader reader, CancellationToken cancellationToken) =>
            {
                var identity = await SalesIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId) || !TryListQuery(context, out var pageSize, out var after)) return InvalidQuery();
                return Results.Ok(await reader.ListAsync(identity, organizationId, branchId, pageSize, after,
                    cancellationToken));
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/{saleId:guid}",
            async (Guid organizationId, Guid branchId, Guid saleId, HttpContext context,
                WebAuthenticationState state, ISaleReader reader, CancellationToken cancellationToken) =>
            {
                var identity = await SalesIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId, saleId)) return InvalidQuery();
                var sale = await reader.ReadAsync(identity, organizationId, branchId, saleId, cancellationToken);
                return sale is null ? Results.NotFound() : Results.Ok(sale);
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/{saleId:guid}/payment",
            async (Guid organizationId, Guid branchId, Guid saleId, HttpContext context,
                WebAuthenticationState state, IPaymentReader reader, CancellationToken cancellationToken) =>
            {
                var identity = await PaymentIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId, saleId)) return InvalidQuery();
                var payment = await reader.ReadForSaleAsync(identity, organizationId, branchId, saleId,
                    cancellationToken);
                return payment is null ? Results.NotFound() : Results.Ok(payment);
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/{saleId:guid}/returns",
            async (Guid organizationId, Guid branchId, Guid saleId, HttpContext context,
                WebAuthenticationState state, IReturnReader reader, CancellationToken cancellationToken) =>
            {
                var identity = await ReturnIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId, saleId) || !TryListQuery(context, out var pageSize, out var after)) return InvalidQuery();
                return Results.Ok(await reader.ListForSaleAsync(identity, organizationId, branchId, saleId,
                    pageSize, after, cancellationToken));
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/{saleId:guid}/void",
            async (Guid organizationId, Guid branchId, Guid saleId, HttpContext context,
                WebAuthenticationState state, ISaleVoidReader reader, CancellationToken cancellationToken) =>
            {
                var identity = await RawIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId, saleId)) return InvalidQuery();
                var result = await reader.ReadForSaleAsync(identity.Value.Issuer, identity.Value.Subject,
                    organizationId, branchId, saleId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/returns",
            async (Guid organizationId, Guid branchId, HttpContext context, WebAuthenticationState state,
                IReturnReader reader, CancellationToken cancellationToken) =>
            {
                var identity = await ReturnIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId) || !TryListQuery(context, out var pageSize, out var after)) return InvalidQuery();
                return Results.Ok(await reader.ListAsync(identity, organizationId, branchId, pageSize, after,
                    cancellationToken));
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/returns/{returnId:guid}",
            async (Guid organizationId, Guid branchId, Guid returnId, HttpContext context,
                WebAuthenticationState state, IReturnReader reader, CancellationToken cancellationToken) =>
            {
                var identity = await ReturnIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId, returnId)) return InvalidQuery();
                var result = await reader.ReadAsync(identity, organizationId, branchId, returnId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/returns/{returnId:guid}/refund",
            async (Guid organizationId, Guid branchId, Guid returnId, HttpContext context,
                WebAuthenticationState state, IPaymentReader reader, CancellationToken cancellationToken) =>
            {
                var identity = await PaymentIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId, returnId)) return InvalidQuery();
                var result = await reader.ReadForReturnAsync(identity, organizationId, branchId, returnId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });

        group.MapPost("/organizations/{organizationId:guid}/branches/{branchId:guid}/returns",
            async (Guid organizationId, Guid branchId, CompleteReturnRequest request, HttpContext context,
                WebAuthenticationState state, IAntiforgery antiforgery, IReturnCompletion completion,
                CancellationToken cancellationToken) =>
            {
                var identity = await ReturnIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId)) return InvalidReturn();
                if (state.Settings is null
                    || !await WebAuthentication.ValidateMutation(context, antiforgery, state.Settings))
                    return Results.BadRequest();
                if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                    || operationId == Guid.Empty) return InvalidReturn();
                try
                {
                    var result = await completion.CompleteAsync(identity, new(organizationId, branchId,
                        Guid.NewGuid(), operationId, request.SaleId, request.Reason, request.Lines), cancellationToken);
                    return result.Created
                        ? Results.Created($"/bff/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/returns/{result.Return.Id:D}", result.Return)
                        : Results.Ok(result.Return);
                }
                catch (ArgumentException) { return InvalidReturn(); }
            });
    }

    public static bool TryListQuery(HttpContext context, out int pageSize, out Guid? after)
    {
        pageSize = 25;
        after = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "after")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out pageSize) || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor)
                || cursor == Guid.Empty) return false;
            after = cursor;
        }
        return true;
    }

    private static async Task<(string Issuer, string Subject)?> RawIdentity(HttpContext context,
        WebAuthenticationState state)
    {
        if (state.Settings is null) return null;
        var result = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        if (!result.Succeeded || result.Principal is null) return null;
        var issuers = result.Principal.FindAll("iss").ToArray();
        var subjects = result.Principal.FindAll("sub").ToArray();
        return issuers.Length == 1 && subjects.Length == 1
            && !string.IsNullOrWhiteSpace(issuers[0].Value) && issuers[0].Value.Length <= 2048
            && !issuers[0].Value.Any(char.IsControl)
            && !string.IsNullOrWhiteSpace(subjects[0].Value) && subjects[0].Value.Length <= 256
            && !subjects[0].Value.Any(char.IsControl)
            ? (issuers[0].Value, subjects[0].Value)
            : null;
    }

    private static async Task<AccessIdentity?> AccessIdentity(HttpContext context, WebAuthenticationState state)
    {
        var identity = await RawIdentity(context, state);
        return identity is null ? null : new(identity.Value.Issuer, identity.Value.Subject);
    }

    private static async Task<SalesIdentity?> SalesIdentity(HttpContext context, WebAuthenticationState state)
    {
        var identity = await RawIdentity(context, state);
        return identity is null ? null : new(identity.Value.Issuer, identity.Value.Subject);
    }

    private static async Task<ReturnIdentity?> ReturnIdentity(HttpContext context, WebAuthenticationState state)
    {
        var identity = await RawIdentity(context, state);
        return identity is null ? null : new(identity.Value.Issuer, identity.Value.Subject);
    }

    private static async Task<PaymentIdentity?> PaymentIdentity(HttpContext context, WebAuthenticationState state)
    {
        var identity = await RawIdentity(context, state);
        return identity is null ? null : new(identity.Value.Issuer, identity.Value.Subject);
    }

    private static IResult Unauthenticated(WebAuthenticationState state) => state.Settings is null
        ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = "web_authentication_unavailable" })
        : Results.Unauthorized();

    private static IResult InvalidQuery() => Results.Problem(statusCode: 400,
        title: "The web business query is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_web_business_query" });

    private static IResult InvalidReturn() => Results.Problem(statusCode: 400,
        title: "The return request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_return_request" });

    private static bool ValidIds(params Guid[] values) => values.All(value => value != Guid.Empty);
}
