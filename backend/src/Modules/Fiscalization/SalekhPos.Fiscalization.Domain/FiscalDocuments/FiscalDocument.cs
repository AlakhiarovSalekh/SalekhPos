using System.Security.Cryptography;
using System.Text;

namespace SalekhPos.Fiscalization.Domain.FiscalDocuments;

public enum FiscalDocumentStatus { Pending, Submitted, Accepted, Rejected }

public sealed record FiscalDocumentDraft
{
    public Guid OrganizationId { get; }
    public Guid BranchId { get; }
    public Guid DocumentId { get; }
    public Guid SaleId { get; }
    public string ProviderKey { get; }
    public string DocumentType { get; }
    public string Currency { get; }
    public decimal GrossAmount { get; }
    public string Payload { get; }
    public string PayloadSha256 { get; }

    public FiscalDocumentDraft(Guid organizationId, Guid branchId, Guid documentId, Guid saleId,
        string providerKey, string documentType, string currency, decimal grossAmount, string payload)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || documentId == Guid.Empty || saleId == Guid.Empty)
            throw new ArgumentException("Fiscal document identifiers are invalid.");
        OrganizationId = organizationId; BranchId = branchId; DocumentId = documentId; SaleId = saleId;
        ProviderKey = Required(providerKey, 80, "provider key");
        DocumentType = Required(documentType, 40, "document type");
        Currency = Required(currency, 3, "currency").ToUpperInvariant();
        if (Currency.Length != 3 || Currency.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Fiscal currency is invalid.");
        if (grossAmount < 0 || decimal.Round(grossAmount, 4) != grossAmount)
            throw new ArgumentException("Fiscal gross amount is invalid.");
        GrossAmount = grossAmount;
        Payload = Required(payload, 262144, "payload");
        PayloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Payload))).ToLowerInvariant();
    }

    private static string Required(string value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > max || value.Any(char.IsControl))
            throw new ArgumentException($"Fiscal {field} is invalid.");
        return value;
    }
}

public sealed record FiscalAttemptEvidence(
    int AttemptNumber,
    string Outcome,
    string? ProviderReference,
    string? ProviderCode,
    string? FailureReason,
    DateTimeOffset AttemptedAt,
    DateTimeOffset? RetryAfter);
