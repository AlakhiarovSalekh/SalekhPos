using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using SalekhPos.Employees.Application.Employees;
using SalekhPos.Employees.Contracts.Employees;

namespace SalekhPos.Employees.Api.Employees;

public static class EmployeeEndpoints
{
    public static void MapEmployeeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/employees")
            .RequireAuthorization("business-api").RequireRateLimiting("business");

        group.MapGet("", async (Guid organizationId, Guid branchId, HttpContext context,
            IEmployeeDirectory employees, CancellationToken cancellationToken) =>
        {
            if (!TryPage(context, out var pageSize, out var after)) return Invalid();
            return Results.Ok(await employees.ListAsync(Identity(context), organizationId, branchId,
                pageSize, after, cancellationToken));
        });

        group.MapGet("/{employeeId:guid}", async (Guid organizationId, Guid branchId, Guid employeeId,
            HttpContext context, IEmployeeDirectory employees, CancellationToken cancellationToken) =>
        {
            var result = await employees.ReadAsync(Identity(context), organizationId, branchId,
                employeeId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("", async (Guid organizationId, Guid branchId, CreateEmployeeRequest request,
            HttpContext context, IEmployeeDirectory employees, IAntiforgery antiforgery,
            CancellationToken cancellationToken) =>
        {
            if (!await ValidateMutation(context, antiforgery)
                || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                || operationId == Guid.Empty) return Invalid();
            try
            {
                var result = await employees.CreateAsync(Identity(context), new(organizationId, branchId,
                    Guid.NewGuid(), operationId, request.Code, request.DisplayName, request.Email,
                    request.Phone, request.JobTitle), cancellationToken);
                var location = $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/employees/{result.Employee.Id:D}";
                return result.Created ? Results.Created(location, result.Employee) : Results.Ok(result.Employee);
            }
            catch (ArgumentException) { return Invalid(); }
        });

        group.MapPut("/{employeeId:guid}", async (Guid organizationId, Guid branchId, Guid employeeId,
            UpdateEmployeeRequest request, HttpContext context, IEmployeeDirectory employees,
            IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        {
            if (!await ValidateMutation(context, antiforgery)) return InvalidBrowserMutation();
            try
            {
                return Results.Ok(await employees.UpdateAsync(Identity(context), new(organizationId, branchId,
                    employeeId, request.DisplayName, request.Email, request.Phone, request.JobTitle,
                    request.IsActive, request.ExpectedVersion), cancellationToken));
            }
            catch (ArgumentException) { return Invalid(); }
        });
    }

    private static EmployeeIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The employee request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_employee_request" });

    private static IResult InvalidBrowserMutation() => Results.Problem(statusCode: 400,
        title: "The browser request could not be verified",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_browser_request" });

    private static async Task<bool> ValidateMutation(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Request.Headers.ContainsKey("Authorization")) return true;
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static bool TryPage(HttpContext context, out int pageSize, out Guid? after)
    {
        pageSize = 50; after = null;
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
}
