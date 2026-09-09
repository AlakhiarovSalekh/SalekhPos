using SalekhPos.Payments.Contracts.Payments;

namespace SalekhPos.Payments.Application.Payments;

public sealed record PaymentIdentity
{
    public string Issuer { get; }
    public string Subject { get; }
    public PaymentIdentity(string issuer, string subject)
    {
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 2048 || issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(subject) || subject.Length > 256 || subject.Any(char.IsControl))
            throw new ArgumentException("Identity is invalid.");
        Issuer = issuer; Subject = subject;
    }
}

public interface IPaymentReader
{
    Task<PaymentResponse?> ReadForSaleAsync(PaymentIdentity identity, Guid organizationId, Guid branchId,
        Guid saleId, CancellationToken cancellationToken);
    Task<RefundResponse?> ReadForReturnAsync(PaymentIdentity identity, Guid organizationId, Guid branchId,
        Guid returnId, CancellationToken cancellationToken);
    Task<RefundResponse?> ReadForVoidAsync(PaymentIdentity identity, Guid organizationId, Guid branchId,
        Guid voidId, CancellationToken cancellationToken);
    Task<PaymentEventPage> ListEventsAsync(PaymentIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, string? after, CancellationToken cancellationToken);
}

public sealed class PaymentDeniedException : Exception;
public sealed class PaymentsUnavailableException : Exception;
