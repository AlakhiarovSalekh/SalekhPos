using SalekhPos.Fiscalization.Contracts.FiscalDocuments;
using SalekhPos.Fiscalization.Domain.FiscalDocuments;

namespace SalekhPos.Fiscalization.Application.FiscalDocuments;

public sealed record FiscalIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Fiscal identity is invalid.");
    }
}

public sealed record FiscalProviderRequest(Guid OrganizationId, Guid BranchId, Guid DocumentId,
    Guid SaleId, string DocumentType, string Currency, decimal GrossAmount, string Payload, string PayloadSha256);

public sealed record FiscalProviderResult(bool Accepted, bool Retryable, string? ProviderReference,
    string? ProviderCode, string? FailureReason, DateTimeOffset? RetryAfter)
{
    public static FiscalProviderResult Accept(string reference, string? code = null) =>
        new(true, false, Required(reference, 200), Optional(code, 80), null, null);
    public static FiscalProviderResult Reject(string code, string reason) =>
        new(false, false, null, Required(code, 80), Required(reason, 1000), null);
    public static FiscalProviderResult Retry(string? code, string reason, DateTimeOffset? retryAfter) =>
        new(false, true, null, Optional(code, 80), Required(reason, 1000), retryAfter);

    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value)
        || value != value.Trim() || value.Length > max || value.Any(char.IsControl)
        ? throw new ArgumentException("Fiscal provider result is invalid.") : value;
    private static string? Optional(string? value, int max) => value is null ? null : Required(value, max);
}

public interface IFiscalProvider
{
    string Key { get; }
    Task<FiscalProviderResult> SubmitAsync(FiscalProviderRequest request, CancellationToken ct);
}

public interface IFiscalProviderRegistry { IFiscalProvider Resolve(string providerKey); }

public interface IFiscalDocumentService
{
    Task<FiscalSubmissionResponse> SubmitAsync(FiscalIdentity identity, Guid organizationId, Guid branchId,
        SubmitFiscalDocumentRequest request, CancellationToken ct);
    Task<FiscalSubmissionResponse> RetryAsync(FiscalIdentity identity, Guid organizationId, Guid documentId,
        CancellationToken ct);
    Task<FiscalDocumentResponse?> ReadAsync(FiscalIdentity identity, Guid organizationId, Guid documentId,
        CancellationToken ct);
}

public sealed class FiscalDeniedException : Exception;
public sealed class FiscalConflictException : Exception;
public sealed class FiscalNotFoundException : Exception;
public sealed class FiscalNotRetryableException : Exception;
public sealed class FiscalProviderNotConfiguredException : Exception;
public sealed class FiscalUnavailableException : Exception;

public sealed class FiscalProviderRegistry(IEnumerable<IFiscalProvider> providers) : IFiscalProviderRegistry
{
    private readonly Dictionary<string, IFiscalProvider> values = providers.ToDictionary(
        provider => provider.Key, StringComparer.Ordinal);

    public IFiscalProvider Resolve(string providerKey) => values.TryGetValue(providerKey, out var provider)
        ? provider : throw new FiscalProviderNotConfiguredException();
}
