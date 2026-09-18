using SalekhPos.Billing.Domain.Accounts;

namespace SalekhPos.Billing.Domain.Invoices;

public enum InvoiceStatus { Draft, Open, PartiallyPaid, Paid, Voided }

public sealed class Invoice
{
    private readonly List<InvoiceLine> _lines = [];
    public Invoice(Guid id, Guid organizationId, Guid accountId, string number, string currency,
        DateTimeOffset issuedAt, DateTimeOffset dueAt)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || accountId == Guid.Empty) throw new ArgumentException("Invoice identifiers are required.");
        if (string.IsNullOrWhiteSpace(number) || number.Trim().Length > 64 || number.Any(char.IsControl)) throw new ArgumentException("Invoice number is invalid.");
        if (issuedAt == default || issuedAt.Offset != TimeSpan.Zero || dueAt.Offset != TimeSpan.Zero || dueAt < issuedAt)
            throw new ArgumentException("Invoice dates are invalid.");
        Id = id; OrganizationId = organizationId; AccountId = accountId; Number = number.Trim();
        Currency = CurrencyCode.Normalize(currency); IssuedAt = issuedAt; DueAt = dueAt;
    }
    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public Guid AccountId { get; }
    public string Number { get; }
    public string Currency { get; }
    public DateTimeOffset IssuedAt { get; }
    public DateTimeOffset DueAt { get; }
    public InvoiceStatus Status { get; private set; }
    public decimal PaidAmount { get; private set; }
    public IReadOnlyList<InvoiceLine> Lines => _lines;
    public decimal NetAmount => _lines.Sum(x => x.NetAmount);
    public decimal TaxAmount => _lines.Sum(x => x.TaxAmount);
    public decimal GrossAmount => NetAmount + TaxAmount;
    public decimal Balance => GrossAmount - PaidAmount;

    public void AddLine(InvoiceLine line)
    {
        if (Status != InvoiceStatus.Draft) throw new InvalidOperationException("Only draft invoices can be changed.");
        if (_lines.Count >= 500 || _lines.Any(x => x.Id == line.Id)) throw new InvalidOperationException("Invoice line cannot be added.");
        _lines.Add(line);
    }
    public void Open()
    {
        if (Status != InvoiceStatus.Draft || _lines.Count == 0 || GrossAmount <= 0) throw new InvalidOperationException("Invoice cannot be opened.");
        Status = InvoiceStatus.Open;
    }
    public void ApplyPayment(decimal amount)
    {
        MoneyGuard.Amount(amount);
        if (Status is not (InvoiceStatus.Open or InvoiceStatus.PartiallyPaid) || amount > Balance)
            throw new InvalidOperationException("Payment cannot be applied.");
        PaidAmount += amount;
        Status = Balance == 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
    }
    public void Void()
    {
        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Open) || PaidAmount != 0) throw new InvalidOperationException("Invoice cannot be voided.");
        Status = InvoiceStatus.Voided;
    }
}
