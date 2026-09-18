using SalekhPos.Support.Domain.Tickets;

namespace SalekhPos.Support.Domain.Diagnostics;

public sealed record DiagnosticReference
{
    public string Kind { get; }
    public string Reference { get; }
    public string Sha256 { get; }

    public DiagnosticReference(string kind, string reference, string sha256)
    {
        Kind = SupportText.Required(kind, 64, nameof(kind)).ToLowerInvariant();
        Reference = SupportText.Required(reference, 512, nameof(reference));
        Sha256 = SupportText.Required(sha256, 64, nameof(sha256)).ToLowerInvariant();
        if (Sha256.Length != 64 || Sha256.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Diagnostic digest must be SHA-256 hexadecimal.", nameof(sha256));
    }
}
