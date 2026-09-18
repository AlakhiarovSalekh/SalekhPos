namespace SalekhPos.Fiscalization.Contracts.FiscalDocuments;

public sealed record SubmitFiscalDocumentRequest(
    Guid DocumentId, Guid SaleId, string ProviderKey, string DocumentType,
    string Currency, decimal GrossAmount, string Payload);

public sealed record FiscalAttemptResponse(int AttemptNumber, string Outcome, string? ProviderReference,
    string? ProviderCode, string? FailureReason, DateTimeOffset AttemptedAt, DateTimeOffset? RetryAfter);

public sealed record FiscalDocumentResponse(Guid DocumentId, Guid BranchId, Guid SaleId, string ProviderKey,
    string DocumentType, string Currency, decimal GrossAmount, string PayloadSha256, string Status,
    string? ProviderReference, int AttemptCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<FiscalAttemptResponse> Attempts);

public sealed record FiscalSubmissionResponse(FiscalDocumentResponse Document, bool Created, bool ProviderCalled);
