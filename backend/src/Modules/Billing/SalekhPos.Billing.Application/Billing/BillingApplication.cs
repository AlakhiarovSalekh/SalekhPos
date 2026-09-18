using SalekhPos.Billing.Contracts.Billing;

namespace SalekhPos.Billing.Application.Billing;

public sealed record BillingIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Billing identity is invalid.");
    }
}

public interface IBillingService
{
    Task<BillingAccountResponse> CreateAccountAsync(BillingIdentity identity, Guid organizationId,
        CreateBillingAccountRequest request, CancellationToken cancellationToken);
    Task<InvoiceResponse> CreateInvoiceAsync(BillingIdentity identity, Guid organizationId,
        CreateInvoiceRequest request, CancellationToken cancellationToken);
    Task<InvoiceResponse> GetInvoiceAsync(BillingIdentity identity, Guid organizationId, Guid invoiceId,
        CancellationToken cancellationToken);
    Task<ChargeResponse> CaptureChargeAsync(BillingIdentity identity, Guid organizationId, Guid invoiceId,
        CaptureChargeRequest request, CancellationToken cancellationToken);
    Task<CreditResponse> IssueCreditAsync(BillingIdentity identity, Guid organizationId, Guid invoiceId,
        IssueCreditRequest request, CancellationToken cancellationToken);
}
public sealed class BillingDeniedException : Exception;
public sealed class BillingConflictException(string message) : Exception(message);
public sealed class BillingNotFoundException : Exception;
public sealed class BillingUnavailableException : Exception;
