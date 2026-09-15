using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using SalekhPos.Payments.Application.Payments;
using SalekhPos.Pricing.Application.Prices;
using SalekhPos.Pricing.Contracts.Prices;
using SalekhPos.Pricing.Domain.Prices;
using SalekhPos.ShiftManagement.Application.Shifts;
using SalekhPos.Stores.Application.Registers;
using SalekhPos.Stores.Contracts.Registers;

namespace SalekhPos.Api.Authentication;

public static class WebOperationsEndpoints
{
    public static void MapWebOperationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff/api/v1")
            .AllowAnonymous()
            .RequireRateLimiting("business");

        MapPricing(group);
        MapRegisters(group);
        MapShifts(group);
        MapPaymentEvents(group);
    }
    private static void MapPricing(RouteGroupBuilder group)
    {
        group.MapPost("/organizations/{organizationId:guid}/pricing/prices",
            async (Guid organizationId, SchedulePriceRequest request, HttpContext context,
                WebAuthenticationState state, IAntiforgery antiforgery, IPriceBook prices,
                CancellationToken cancellationToken) =>
            {
                var identity = await PricingIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId) || !await ValidMutation(context, state, antiforgery)
                    || !TryOperationId(context, out var operationId)
                    || !TryMode(request.TaxMode, out var mode)) return Invalid("invalid_pricing_request");
                try
                {
                    var result = await prices.ScheduleAsync(identity, new(organizationId, Guid.NewGuid(),
                        operationId, request.ProductId, request.BranchId, request.Amount, request.Currency,
                        mode, request.TaxRate, request.ValidFrom, request.ValidUntil), cancellationToken);
                    var location = $"/bff/api/v1/organizations/{organizationId:D}/pricing/prices/{result.Price.Id:D}";
                    return result.Created ? Results.Created(location, result.Price) : Results.Ok(result.Price);
                }
                catch (ArgumentException) { return Invalid("invalid_pricing_request"); }
            });
        group.MapGet("/organizations/{organizationId:guid}/pricing/resolve",
            async (Guid organizationId, HttpContext context, WebAuthenticationState state,
                IPriceBook prices, CancellationToken cancellationToken) =>
            {
                var identity = await PricingIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId)
                    || context.Request.Query.Keys.Any(key => key is not "branchId" and not "productId" and not "at")
                    || !Guid.TryParseExact(context.Request.Query["branchId"], "D", out var branchId)
                    || !Guid.TryParseExact(context.Request.Query["productId"], "D", out var productId)
                    || !ValidIds(branchId, productId)
                    || !DateTimeOffset.TryParseExact(context.Request.Query["at"], "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var at) || at.Offset != TimeSpan.Zero)
                    return Invalid("invalid_pricing_query");
                var result = await prices.ResolveAsync(identity, organizationId, branchId, productId, at,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
    }

    private static void MapRegisters(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/registers",
            async (Guid organizationId, Guid branchId, HttpContext context, WebAuthenticationState state,
                IRegisterCatalog registers, CancellationToken cancellationToken) =>
            {
                var identity = await StoreIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId)
                    || !WebBusinessEndpoints.TryListQuery(context, out var pageSize, out var after))
                    return Invalid("invalid_register_query");
                return Results.Ok(await registers.ListAsync(identity, organizationId, branchId, pageSize, after,
                    cancellationToken));
            });

        group.MapPost("/organizations/{organizationId:guid}/branches/{branchId:guid}/registers",
            async (Guid organizationId, Guid branchId, CreateRegisterRequest request, HttpContext context,
                WebAuthenticationState state, IAntiforgery antiforgery, IRegisterCatalog registers,
                CancellationToken cancellationToken) =>
            {
                var identity = await StoreIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId) || !await ValidMutation(context, state, antiforgery)
                    || !TryOperationId(context, out var operationId)) return Invalid("invalid_register_request");
                try
                {
                    var result = await registers.CreateAsync(identity, new(organizationId, branchId, Guid.NewGuid(),
                        operationId, request.Code, request.Name), cancellationToken);
                    var location = $"/bff/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/registers/{result.Register.Id:D}";
                    return result.Created ? Results.Created(location, result.Register) : Results.Ok(result.Register);
                }
                catch (ArgumentException) { return Invalid("invalid_register_request"); }
            });
    }

    private static void MapShifts(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/shifts/open",
            async (Guid organizationId, Guid branchId, HttpContext context, WebAuthenticationState state,
                IShiftService shifts, CancellationToken cancellationToken) =>
            {
                var identity = await ShiftIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId)
                    || context.Request.Query.Keys.Any(key => key != "registerId")
                    || !Guid.TryParseExact(context.Request.Query["registerId"], "D", out var registerId)
                    || !ValidIds(registerId)) return Invalid("invalid_shift_query");
                var result = await shifts.ReadOpenAsync(identity, organizationId, branchId, registerId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/shifts/{shiftId:guid}/cash-movements",
            async (Guid organizationId, Guid branchId, Guid shiftId, HttpContext context,
                WebAuthenticationState state, IShiftService shifts, CancellationToken cancellationToken) =>
            {
                var identity = await ShiftIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId, shiftId) || context.Request.Query.Count != 0)
                    return Invalid("invalid_shift_query");
                return Results.Ok(await shifts.ListCashMovementsAsync(identity, organizationId, branchId, shiftId,
                    cancellationToken));
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/shifts/closed",
            async (Guid organizationId, Guid branchId, HttpContext context, WebAuthenticationState state,
                IShiftService shifts, CancellationToken cancellationToken) =>
            {
                var identity = await ShiftIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId)
                    || !WebBusinessEndpoints.TryListQuery(context, out var pageSize, out var after))
                    return Invalid("invalid_shift_query");
                return Results.Ok(await shifts.ListClosedAsync(identity, organizationId, branchId, pageSize, after,
                    cancellationToken));
            });

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/shifts/{shiftId:guid}",
            async (Guid organizationId, Guid branchId, Guid shiftId, HttpContext context,
                WebAuthenticationState state, IShiftService shifts, CancellationToken cancellationToken) =>
            {
                var identity = await ShiftIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId, shiftId) || context.Request.Query.Count != 0)
                    return Invalid("invalid_shift_query");
                var result = await shifts.ReadClosedAsync(identity, organizationId, branchId, shiftId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
    }

    private static void MapPaymentEvents(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/payment-events",
            async (Guid organizationId, Guid branchId, HttpContext context, WebAuthenticationState state,
                IPaymentReader payments, CancellationToken cancellationToken) =>
            {
                var identity = await PaymentIdentity(context, state);
                if (identity is null) return Unauthenticated(state);
                if (!ValidIds(organizationId, branchId)
                    || !TryOpaqueListQuery(context, out var pageSize, out var after))
                    return Invalid("invalid_payment_event_query");
                return Results.Ok(await payments.ListEventsAsync(identity, organizationId, branchId, pageSize, after,
                    cancellationToken));
            });
    }

    private static async Task<(string Issuer, string Subject)?> RawIdentity(HttpContext context,
        WebAuthenticationState state)
    {
        if (state.Settings is null) return null;
        var result = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        if (!result.Succeeded || result.Principal is null) return null;
        var issuers = result.Principal.FindAll("iss").ToArray();
        var subjects = result.Principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1) return null;
        var issuer = issuers[0].Value;
        var subject = subjects[0].Value;
        return !string.IsNullOrWhiteSpace(issuer) && issuer.Length <= 2048 && !issuer.Any(char.IsControl)
            && !string.IsNullOrWhiteSpace(subject) && subject.Length <= 256 && !subject.Any(char.IsControl)
            ? (issuer, subject) : null;
    }

    private static async Task<PricingIdentity?> PricingIdentity(HttpContext context, WebAuthenticationState state)
    {
        var identity = await RawIdentity(context, state);
        return identity is null ? null : new(identity.Value.Issuer, identity.Value.Subject);
    }

    private static async Task<StoreIdentity?> StoreIdentity(HttpContext context, WebAuthenticationState state)
    {
        var identity = await RawIdentity(context, state);
        return identity is null ? null : new(identity.Value.Issuer, identity.Value.Subject);
    }

    private static async Task<ShiftIdentity?> ShiftIdentity(HttpContext context, WebAuthenticationState state)
    {
        var identity = await RawIdentity(context, state);
        return identity is null ? null : new(identity.Value.Issuer, identity.Value.Subject);
    }
    private static async Task<PaymentIdentity?> PaymentIdentity(HttpContext context, WebAuthenticationState state)
    {
        var identity = await RawIdentity(context, state);
        return identity is null ? null : new(identity.Value.Issuer, identity.Value.Subject);
    }

    private static async Task<bool> ValidMutation(HttpContext context, WebAuthenticationState state,
        IAntiforgery antiforgery) => state.Settings is not null
        && await WebAuthentication.ValidateMutation(context, antiforgery, state.Settings);

    private static bool TryOperationId(HttpContext context, out Guid operationId) =>
        Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out operationId)
        && operationId != Guid.Empty;

    private static bool TryMode(string? value, out TaxMode mode)
    {
        mode = value == "inclusive" ? TaxMode.Inclusive : TaxMode.Exclusive;
        return value is "inclusive" or "exclusive";
    }

    private static bool TryOpaqueListQuery(HttpContext context, out int pageSize, out string? after)
    {
        pageSize = 25;
        after = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "after")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture,
                out pageSize) || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || string.IsNullOrWhiteSpace(cursors[0]) || cursors[0]!.Length > 512
                || cursors[0]!.Any(char.IsControl)) return false;
            after = cursors[0];
        }
        return true;
    }

    private static IResult Unauthenticated(WebAuthenticationState state) => state.Settings is null
        ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = "web_authentication_unavailable" })
        : Results.Unauthorized();

    private static IResult Invalid(string code) => Results.Problem(statusCode: 400,
        title: "The web operations request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static bool ValidIds(params Guid[] values) => values.All(value => value != Guid.Empty);
}
