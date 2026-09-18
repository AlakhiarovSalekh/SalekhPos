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
        ArgumentOutOfRangeException.ThrowIfNegative(unixTimestampSeconds);

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

        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(signature[3..]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedText = Sign(secret, unixTimestampSeconds, payload);
        var expected = Convert.FromHexString(expectedText[3..]);
        return supplied.Length == 32
            && expected.Length == 32
            && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private static void ValidateSecret(ReadOnlySpan<byte> secret)
    {
        if (secret.Length is < 32 or > 4096)
        {
            throw new ArgumentException("Webhook signing keys must contain between 32 and 4096 bytes.", nameof(secret));
        }
    }
}
