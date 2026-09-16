using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Taxation.Application.TaxConfiguration;
using SalekhPos.Taxation.Contracts.TaxConfiguration;

namespace SalekhPos.Taxation.Api.TaxConfiguration;

public static class TaxEndpoints
{
    public static void MapTaxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/tax-profiles")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("", ListProfiles);
        group.MapPost("", CreateProfile);
        group.MapGet("/{profileId:guid}/rates", ListRates);
        group.MapPost("/{profileId:guid}/rates", CreateRate);
        group.MapPost("/calculate", Calculate);
    }
    private static TaxIdentity Identity(HttpContext c) => new(c.User.FindFirst("iss")!.Value, c.User.FindFirst("sub")!.Value);
    private static async Task<IResult> ListProfiles(Guid organizationId, HttpContext c, ITaxConfiguration tax, CancellationToken ct)
    {
        var pageSize = 50; Guid? after = null; if (c.Request.Query.TryGetValue("pageSize", out var p) && (!int.TryParse(p, out pageSize) || pageSize is < 1 or > 100)) return Invalid();
        if (c.Request.Query.TryGetValue("after", out var a)) { if (!Guid.TryParse(a, out var parsed) || parsed == Guid.Empty) return Invalid(); after = parsed; }
        return Results.Ok(await tax.ListProfilesAsync(Identity(c), organizationId, pageSize, after, ct));
    }
    private static async Task<IResult> CreateProfile(Guid organizationId, CreateTaxProfileRequest request, HttpContext c, ITaxConfiguration tax, IAntiforgery antiforgery, CancellationToken ct)
    {
        if (!await Mutation(c, antiforgery) || !Guid.TryParseExact(c.Request.Headers["Idempotency-Key"], "D", out var op) || op == Guid.Empty) return Invalid();
        try
        {
            var result = await tax.CreateProfileAsync(Identity(c), new(organizationId, Guid.NewGuid(), op, request.Code, request.Name, request.CountryCode, request.PricesIncludeTax), ct);
            return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/tax-profiles/{result.Profile.Id:D}", result.Profile) : Results.Ok(result.Profile);
        }
        catch (ArgumentException) { return Invalid(); }
    }
    private static async Task<IResult> ListRates(Guid organizationId, Guid profileId, HttpContext c, ITaxConfiguration tax, CancellationToken ct)
        => Results.Ok(await tax.ListRatesAsync(Identity(c), organizationId, profileId, ct));

    private static async Task<IResult> CreateRate(Guid organizationId, Guid profileId, CreateTaxRateRequest request, HttpContext c,
        ITaxConfiguration tax, IAntiforgery antiforgery, CancellationToken ct)
    {
        if (!await Mutation(c, antiforgery) || !Guid.TryParseExact(c.Request.Headers["Idempotency-Key"], "D", out var op) || op == Guid.Empty) return Invalid();
        try
        {
            var result = await tax.CreateRateAsync(Identity(c), new(organizationId, Guid.NewGuid(), op, profileId, request.BranchId,
            request.CategoryCode, request.RatePercent, request.EffectiveFrom, request.EffectiveUntil), ct);
            return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/tax-profiles/{profileId:D}/rates/{result.Rate.Id:D}", result.Rate) : Results.Ok(result.Rate);
        }
        catch (ArgumentException) { return Invalid(); }
    }

    private static async Task<IResult> Calculate(Guid organizationId, CalculateTaxRequest request, HttpContext c, ITaxConfiguration tax, CancellationToken ct)
    {
        try { return Results.Ok(await tax.CalculateAsync(Identity(c), organizationId, request, ct)); } catch (ArgumentException) { return Invalid(); }
    }
    private static async Task<bool> Mutation(HttpContext c, IAntiforgery antiforgery)
    {
        if (c.Request.Headers.ContainsKey("Authorization")) return true;
        try { await antiforgery.ValidateRequestAsync(c); return true; } catch (AntiforgeryValidationException) { return false; }
    }
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The tax request is invalid",
        extensions: new Dictionary<string, object?> { { "code", "invalid_tax_request" } });
}
