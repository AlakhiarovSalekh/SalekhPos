using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using SalekhPos.FeatureManagement.Application.Features;
using SalekhPos.FeatureManagement.Contracts.Features;
namespace SalekhPos.Api.Authentication;

public static class WebFeatureManagementEndpoints
{
    public static void MapWebFeatureManagementEndpoints(this WebApplication app) { var g = app.MapGroup("/bff/api/v1").AllowAnonymous().RequireRateLimiting("business"); g.MapGet("/organizations/{organizationId:guid}/features/{key}", Evaluate); g.MapPut("/organizations/{organizationId:guid}/features/{key}/override", Override); }
    private static async Task<IResult> Evaluate(Guid organizationId, string key, HttpContext c, WebAuthenticationState s, IFeaturePolicyService service, CancellationToken ct) { var i = await Identity(c, s); return i is null ? Unauth(s) : Results.Ok(await service.EvaluateAsync(i, organizationId, key, ct)); }
    private static async Task<IResult> Override(Guid organizationId, string key, FeatureOverrideRequest request, HttpContext c, WebAuthenticationState s, IAntiforgery a, IFeaturePolicyService service, CancellationToken ct) { var i = await Identity(c, s); if (i is null) return Unauth(s); if (!await WebAuthentication.ValidateMutation(c, a, s.Settings!)) return Invalid(); try { return Results.Ok(await service.SetOverrideAsync(i, organizationId, key, request.Enabled, request.Reason, ct)); } catch (ArgumentException) { return Invalid(); } }
    private static async Task<FeatureIdentity?> Identity(HttpContext c, WebAuthenticationState s) { if (s.Settings is null) return null; var r = await c.AuthenticateAsync(WebAuthentication.CookieScheme); if (!r.Succeeded || r.Principal is null) return null; var iss = r.Principal.FindAll("iss").ToArray(); var sub = r.Principal.FindAll("sub").ToArray(); if (iss.Length != 1 || sub.Length != 1) return null; try { var i = new FeatureIdentity(iss[0].Value, sub[0].Value); i.Validate(); return i; } catch (ArgumentException) { return null; } }
    private static IResult Unauth(WebAuthenticationState s) => s.Settings is null ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable", extensions: new Dictionary<string, object?> { { "code", "web_authentication_unavailable" } }) : Results.Unauthorized();
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The feature request is invalid", extensions: new Dictionary<string, object?> { { "code", "invalid_feature_request" } });
}
