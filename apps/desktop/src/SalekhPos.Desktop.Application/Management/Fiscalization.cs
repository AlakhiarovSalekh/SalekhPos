namespace SalekhPos.Desktop.Application.Management;

public sealed record DesktopFiscalAttempt(int AttemptNumber, string Outcome, string? ProviderReference, string? ProviderCode, string? FailureReason, DateTimeOffset AttemptedAt, DateTimeOffset? RetryAfter);
public sealed record DesktopFiscalDocument(Guid DocumentId, Guid BranchId, Guid SaleId, string ProviderKey, string DocumentType, string Currency, decimal GrossAmount, string PayloadSha256, string Status, string? ProviderReference, int AttemptCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<DesktopFiscalAttempt> Attempts);
public sealed record DesktopFiscalSubmission(DesktopFiscalDocument Document, bool Created, bool ProviderCalled);
public sealed record DesktopFiscalInput(Guid DocumentId, Guid SaleId, string ProviderKey, string DocumentType, string Currency, decimal GrossAmount, string Payload);
public interface IFiscalizationManager { Task<DesktopFiscalDocument> ReadAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken); Task<DesktopFiscalSubmission> SubmitAsync(Guid organizationId, Guid branchId, DesktopFiscalInput input, CancellationToken cancellationToken); Task<DesktopFiscalSubmission> RetryAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken); }
