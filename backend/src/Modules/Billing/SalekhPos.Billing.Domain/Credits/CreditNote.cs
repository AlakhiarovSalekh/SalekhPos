using SalekhPos.Billing.Domain.Accounts;

namespace SalekhPos.Billing.Domain.Credits;

public sealed record CreditNote
{
    public CreditNote(Guid id, Guid organizationId, Guid invoiceId, Guid operationId, decimal amount,
        string currency, string reason, DateTimeOffset issuedAt)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || invoiceId == Guid.Empty || operationId == Guid.Empty) throw new ArgumentException("Credit identifiers are required.");
        Invoices.MoneyGuard.Amount(amount);
        if (issuedAt == default || issuedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Issued timestamp must be UTC.");
        Id = id; OrganizationId = organizationId; InvoiceId = invoiceId; OperationId = operationId;
        Amount = amount; Currency = CurrencyCode.Normalize(currency);
        Reason = BillingAccount.Required(reason, 240, "Credit reason"); IssuedAt = issuedAt;
    }
    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public Guid InvoiceId { get; }
    public Guid OperationId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public string Reason { get; }
    public DateTimeOffset IssuedAt { get; }
}
