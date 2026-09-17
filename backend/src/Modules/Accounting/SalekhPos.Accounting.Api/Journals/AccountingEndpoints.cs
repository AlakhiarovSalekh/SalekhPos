using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SalekhPos.Accounting.Application.Journals;
using SalekhPos.Accounting.Domain.Journals;

namespace SalekhPos.Accounting.Api.Journals;

public static class AccountingEndpoints
{
    public static void MapAccountingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(
                "/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/accounting")
            .RequireAuthorization("business-api")
            .RequireRateLimiting("business");
        group.MapGet("/summary", Summary);
        group.MapGet("/journal", Journal);
    }

    private static async Task<IResult> Summary(Guid organizationId, Guid branchId,
        HttpContext context, IAccountingReader accounting, CancellationToken cancellationToken)
    {
        if (!TryWindow(context, allowPaging: false, out var window, out _, out _)) return Invalid();
        return Results.Ok(await accounting.ReadSummaryAsync(
            Identity(context), organizationId, branchId, window, cancellationToken));
    }

    private static async Task<IResult> Journal(Guid organizationId, Guid branchId,
        HttpContext context, IAccountingReader accounting, CancellationToken cancellationToken)
    {
        if (!TryWindow(context, allowPaging: true, out var window, out var pageSize, out var cursor))
            return Invalid();
        return Results.Ok(await accounting.ReadJournalAsync(
            Identity(context), organizationId, branchId, window, pageSize, cursor, cancellationToken));
    }

    private static AccountingIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

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
        if (!TryInstant(context, "from", out var from) || !TryInstant(context, "to", out var to))
            return false;
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

    private static IResult Invalid() => Results.Problem(
        statusCode: 400,
        title: "The accounting request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_accounting_request" });
}
