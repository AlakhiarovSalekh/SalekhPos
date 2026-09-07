using System.Security.Cryptography;
using System.Text;

namespace SalekhPos.Identity.Application;

// Construct only after signature, issuer, audience and lifetime validation.
// The raw bearer credential is never retained, persisted or logged here.
public sealed class AuthenticatedCredential
{
    public string Issuer { get; }
    public string Subject { get; }
    public string Fingerprint { get; }
    public DateTime ExpiresAtUtc { get; }

    public AuthenticatedCredential(string issuer, string subject, string encodedToken, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 2048 || issuer.Any(char.IsControl))
        {
            throw new ArgumentException("Invalid issuer.", nameof(issuer));
        }
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 256 || subject.Any(char.IsControl))
        {
            throw new ArgumentException("Invalid subject.", nameof(subject));
        }
        if (string.IsNullOrWhiteSpace(encodedToken) || encodedToken.Length > 16384)
        {
            throw new ArgumentException("Invalid credential.", nameof(encodedToken));
        }
        if (expiresAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Expiry must be UTC.", nameof(expiresAtUtc));
        }
        var signatureSeparator = encodedToken.LastIndexOf('.');
        if (signatureSeparator <= 0 || signatureSeparator == encodedToken.Length - 1)
        {
            throw new ArgumentException("A signed compact token is required.", nameof(encodedToken));
        }
        Issuer = issuer;
        Subject = subject;
        // Fingerprint the exact signed header.payload, not the signature's textual
        // encoding. Alternate base64url encodings of the same signature must not
        // bypass revocation. Identical signed claims are the same credential here.
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(encodedToken[..signatureSeparator])));
        ExpiresAtUtc = expiresAtUtc;
    }
}

public sealed class IdentityUnavailableException : Exception;
