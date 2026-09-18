using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SalekhPos.Integrations.Domain.Security;

public static class WebhookSignature
{
    public const string Version = "v1";

    public static string Sign(ReadOnlySpan<byte> secret, long unixTimestampSeconds, ReadOnlySpan<byte> payload)
    {
        ValidateSecret(secret);
        if (unixTimestampSeconds < 0) throw new ArgumentOutOfRangeException(nameof(unixTimestampSeconds));

        var prefix = Encoding.ASCII.GetBytes(unixTimestampSeconds.ToString(CultureInfo.InvariantCulture) + ".");
        var canonical = GC.AllocateUninitializedArray<byte>(checked(prefix.Length + payload.Length));
        prefix.CopyTo(canonical, 0);
        payload.CopyTo(canonical.AsSpan(prefix.Length));

        using var hmac = new HMACSHA256(secret.ToArray());
        var digest = hmac.ComputeHash(canonical);
        CryptographicOperations.ZeroMemory(canonical);
        return Version + "=" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    public static bool Verify(ReadOnlySpan<byte> secret, long unixTimestampSeconds, ReadOnlySpan<byte> payload,
        string? signature)
    {
        ValidateSecret(secret);
        if (signature is null || signature.Length != 67 || !signature.StartsWith(Version + "=", StringComparison.Ordinal))
        {
            return false;
        }

        Span<byte> supplied = stackalloc byte[32];
        if (!Convert.TryFromHexString(signature.AsSpan(3), supplied, out var written) || written != supplied.Length)
        {
            return false;
        }

        var expectedText = Sign(secret, unixTimestampSeconds, payload);
        Span<byte> expected = stackalloc byte[32];
        _ = Convert.TryFromHexString(expectedText.AsSpan(3), expected, out _);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private static void ValidateSecret(ReadOnlySpan<byte> secret)
    {
        if (secret.Length is < 32 or > 4096)
        {
            throw new ArgumentException("Webhook signing keys must contain between 32 and 4096 bytes.", nameof(secret));
        }
    }
}
