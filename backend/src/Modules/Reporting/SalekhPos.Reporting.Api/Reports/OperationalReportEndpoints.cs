using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;
using SalekhPos.Reporting.Application.Reports;
using SalekhPos.Reporting.Domain.SalesReports;

namespace SalekhPos.Reporting.Api.Reports;

public static class OperationalReportEndpoints
{
    public static void MapOperationalReportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/reports/operational-summary",
            async (Guid organizationId, Guid branchId, HttpContext context, IOperationalReportService reports,
                CancellationToken cancellationToken) =>
            {
                if (!TryWindow(context, out var window)) return Invalid();
                var result = await reports.ReadSummaryAsync(Identity(context), organizationId, branchId,
                    window!, cancellationToken);
                return Results.Ok(result);
            }).RequireAuthorization("business-api").RequireRateLimiting("business");
    }

    private static ReportingIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static bool TryWindow(HttpContext context, out ReportWindow? window)
    {
        window = null;
        if (context.Request.Query.Keys.Any(key => key is not "from" and not "to")) return false;
        if (!context.Request.Query.TryGetValue("from", out var fromValues) || fromValues.Count != 1
            || !context.Request.Query.TryGetValue("to", out var toValues) || toValues.Count != 1)
            return false;
        if (!DateTimeOffset.TryParseExact(fromValues[0], "O", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var from)
            || !DateTimeOffset.TryParseExact(toValues[0], "O", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var to)) return false;
        try { window = new(from, to); return true; }
        catch (ArgumentException) { return false; }
    }

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The report request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_report_request" });
}
