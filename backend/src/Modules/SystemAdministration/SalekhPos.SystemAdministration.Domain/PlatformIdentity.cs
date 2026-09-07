using System.Text;

namespace SalekhPos.SystemAdministration.Domain;

public sealed record PlatformIdentity
{
    public string Issuer { get; }
    public string Subject { get; }

    public PlatformIdentity(string issuer, string subject)
    {
        Issuer = PlatformInput.Text(issuer, 2048);
        Subject = PlatformInput.Text(subject, 256);
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("An HTTPS identity issuer is required.", nameof(issuer));
        }
    }
}

public static class PlatformInput
{
    public static string Text(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > maximumLength
            || value.Any(char.IsControl))
        {
            throw new ArgumentException("Invalid platform input.", nameof(value));
        }
        try { _ = new UTF8Encoding(false, true).GetByteCount(value); }
        catch (EncoderFallbackException) { throw new ArgumentException("Invalid Unicode input.", nameof(value)); }
        return value;
    }

    public static Guid Identifier(Guid value) => value != Guid.Empty ? value : throw new ArgumentException("An operation identifier is required.", nameof(value));
}
