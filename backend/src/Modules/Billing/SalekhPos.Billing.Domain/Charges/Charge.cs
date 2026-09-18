using SalekhPos.Billing.Domain.Accounts;

namespace SalekhPos.Billing.Domain.Charges;

public enum ChargeStatus { Pending, Succeeded, Failed, Refunded }

public sealed class Charge
{
    public Charge(Guid id, Guid organizationId, Guid invoiceId, Guid operationId, decimal amount, string currency, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || invoiceId == Guid.Empty || operationId == Guid.Empty) throw new ArgumentException("Charge identifiers are required.");
        Invoices.MoneyGuard.Amount(amount);
        if (createdAt == default || createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Created timestamp must be UTC.");
        Id = id; OrganizationId = organizationId; InvoiceId = invoiceId; OperationId = operationId;
        Amount = amount; Currency = CurrencyCode.Normalize(currency); CreatedAt = createdAt;
    }
    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public Guid InvoiceId { get; }
    public Guid OperationId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public DateTimeOffset CreatedAt { get; }
    public ChargeStatus Status { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? FailureCode { get; private set; }
    public void Succeed(string providerReference)
    {
        if (Status != ChargeStatus.Pending) throw new InvalidOperationException("Charge is final.");
        ProviderReference = Accounts.BillingAccount.Required(providerReference, 128, "Provider reference"); Status = ChargeStatus.Succeeded;
    }
    public void Fail(string code)
    {
        if (Status != ChargeStatus.Pending) throw new InvalidOperationException("Charge is final.");
        FailureCode = Accounts.BillingAccount.Required(code, 64, "Failure code"); Status = ChargeStatus.Failed;
    }
    public void Refund()
    {
        if (Status != ChargeStatus.Succeeded) throw new InvalidOperationException("Only successful charges can be refunded.");
        Status = ChargeStatus.Refunded;
    }
}
