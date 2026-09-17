namespace SalekhPos.Billing.Contracts.Billing;

public sealed record CreateBillingAccountRequest(Guid OperationId, Guid AccountId, string LegalName,
    string BillingEmail, string Currency, string? TaxIdentifier);
public sealed record BillingAccountResponse(Guid AccountId, string LegalName, string BillingEmail,
    string Currency, string? TaxIdentifier, DateTimeOffset CreatedAt);
public sealed record InvoiceLineRequest(Guid LineId, string Description, long Quantity, decimal UnitAmount, decimal TaxRate);
public sealed record CreateInvoiceRequest(Guid OperationId, Guid InvoiceId, Guid AccountId, string Number,
    string Currency, DateTimeOffset IssuedAt, DateTimeOffset DueAt, IReadOnlyList<InvoiceLineRequest> Lines);
public sealed record InvoiceResponse(Guid InvoiceId, Guid AccountId, string Number, string Currency, string Status,
    decimal NetAmount, decimal TaxAmount, decimal GrossAmount, decimal PaidAmount, decimal Balance,
    DateTimeOffset IssuedAt, DateTimeOffset DueAt);
public sealed record CaptureChargeRequest(Guid OperationId, Guid ChargeId, decimal Amount, string Currency, string ProviderReference);
public sealed record ChargeResponse(Guid ChargeId, Guid InvoiceId, string Status, decimal Amount, string Currency, string? ProviderReference);
public sealed record IssueCreditRequest(Guid OperationId, Guid CreditId, decimal Amount, string Currency, string Reason);
public sealed record CreditResponse(Guid CreditId, Guid InvoiceId, decimal Amount, string Currency, string Reason, DateTimeOffset IssuedAt);
