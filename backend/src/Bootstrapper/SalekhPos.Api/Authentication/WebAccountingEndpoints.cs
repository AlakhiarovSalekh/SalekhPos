using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using SalekhPos.Accounting.Application.Journals;
using SalekhPos.Accounting.Domain.Journals;

namespace SalekhPos.Api.Authentication;

public static class WebAccountingEndpoints
{
    public static void MapWebAccountingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff/api/v1")
            .AllowAnonymous()
            .RequireRateLimiting("business");
        group.MapGet(
            "/organizations/{organizationId:guid}/branches/{branchId:guid}/accounting/summary", Summary);
        group.MapGet(
            "/organizations/{organizationId:guid}/branches/{branchId:guid}/accounting/journal", Journal);
    }

    private static async Task<IResult> Summary(Guid organizationId, Guid branchId,
        HttpContext context, WebAuthenticationState state, IAccountingReader accounting,
        CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!TryWindow(context, false, out var window, out _, out _)) return Invalid();
        return Results.Ok(await accounting.ReadSummaryAsync(identity, organizationId,
            branchId, window, cancellationToken));
    }

    private static async Task<IResult> Journal(Guid organizationId, Guid branchId,
        HttpContext context, WebAuthenticationState state, IAccountingReader accounting,
        CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!TryWindow(context, true, out var window, out var pageSize, out var cursor))
            return Invalid();
        return Results.Ok(await accounting.ReadJournalAsync(identity, organizationId,
            branchId, window, pageSize, cursor, cancellationToken));
    }

    private static async Task<AccountingIdentity?> Identity(
        HttpContext context, WebAuthenticationState state)
    {
        if (state.Settings is null) return null;
        var result = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        if (!result.Succeeded || result.Principal is null) return null;
        var issuers = result.Principal.FindAll("iss").ToArray();
        var subjects = result.Principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1) return null;
        try
        {
            var value = new AccountingIdentity(issuers[0].Value, subjects[0].Value);
            value.Validate();
            return value;
        }
        catch (ArgumentException) { return null; }
    }

    private static bool TryWindow(HttpContext context, bool allowPaging,
        out AccountingWindow window, out int pageSize, out string? cursor)
    {
        window = null!;
        pageSize = 50;
        cursor = null;
        if (context.Request.Query.Keys.Any(key => key is not "from" and not "to"
            and not "pageSize" and not "cursor")) return false;
        if (!allowPaging && (context.Request.Query.ContainsKey("pageSize")
            || context.Request.Query.ContainsKey("cursor"))) return false;
        if (!TryInstant(context, "from", out var from)
            || !TryInstant(context, "to", out var to)) return false;
        try { window = new(from, to); }
        catch (ArgumentException) { return false; }
        if (allowPaging && context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out pageSize) || pageSize is < 1 or > 100)) return false;
        if (allowPaging && context.Request.Query.TryGetValue("cursor", out var cursors))
        {
            if (cursors.Count != 1 || string.IsNullOrWhiteSpace(cursors[0])
                || cursors[0]!.Length > 256 || cursors[0]!.Any(char.IsControl)) return false;
            cursor = cursors[0];
        }
        return true;
    }

    private static bool TryInstant(HttpContext context, string name, out DateTimeOffset value)
    {
        value = default;
        return context.Request.Query.TryGetValue(name, out var values) && values.Count == 1
            && DateTimeOffset.TryParse(values[0], CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out value) && value.Offset == TimeSpan.Zero;
    }

    private static IResult Unauthenticated(WebAuthenticationState state) => state.Settings is null
        ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = "web_authentication_unavailable" })
        : Results.Unauthorized();

    private static IResult Invalid() => Results.Problem(
        statusCode: 400,
        title: "The accounting request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_accounting_request" });
}
