using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SalekhPos.Billing.Application.Billing;
using SalekhPos.Billing.Contracts.Billing;

namespace SalekhPos.Billing.Api.Billing;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/billing")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapPost("/accounts", CreateAccount);
        group.MapPost("/invoices", CreateInvoice);
        group.MapGet("/invoices/{invoiceId:guid}", GetInvoice);
        group.MapPost("/invoices/{invoiceId:guid}/charges", CaptureCharge);
        group.MapPost("/invoices/{invoiceId:guid}/credits", IssueCredit);
    }
    private static Task<BillingAccountResponse> CreateAccount(Guid organizationId, CreateBillingAccountRequest request,
        HttpContext context, IBillingService service, CancellationToken cancellationToken) =>
        service.CreateAccountAsync(Identity(context), organizationId, request, cancellationToken);
    private static Task<InvoiceResponse> CreateInvoice(Guid organizationId, CreateInvoiceRequest request,
        HttpContext context, IBillingService service, CancellationToken cancellationToken) =>
        service.CreateInvoiceAsync(Identity(context), organizationId, request, cancellationToken);
    private static Task<InvoiceResponse> GetInvoice(Guid organizationId, Guid invoiceId, HttpContext context,
        IBillingService service, CancellationToken cancellationToken) =>
        service.GetInvoiceAsync(Identity(context), organizationId, invoiceId, cancellationToken);
    private static Task<ChargeResponse> CaptureCharge(Guid organizationId, Guid invoiceId, CaptureChargeRequest request,
        HttpContext context, IBillingService service, CancellationToken cancellationToken) =>
        service.CaptureChargeAsync(Identity(context), organizationId, invoiceId, request, cancellationToken);
    private static Task<CreditResponse> IssueCredit(Guid organizationId, Guid invoiceId, IssueCreditRequest request,
        HttpContext context, IBillingService service, CancellationToken cancellationToken) =>
        service.IssueCreditAsync(Identity(context), organizationId, invoiceId, request, cancellationToken);
    private static BillingIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
}
