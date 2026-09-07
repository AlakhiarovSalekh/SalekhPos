using System.Text;

namespace SalekhPos.Organizations;

internal static class OrganizationRules
{
    public static string ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || IsWhitespace(name[0]) || IsWhitespace(name[^1])
            || name.EnumerateRunes().Count() > 200 || name.Any(IsControl))
        {
            throw new ArgumentException("Name must contain 1–200 Unicode characters without outer whitespace or control characters.", nameof(name));
        }
        // Reject malformed UTF-16 rather than allowing replacement characters on persistence.
        try
        {
            _ = new UTF8Encoding(false, true).GetByteCount(name);
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException("Name must contain valid Unicode text.", nameof(name));
        }
        return name;
    }

    // Explicit Unicode White_Space and C0/C1 sets are mirrored by migration 002.
    // Do not depend on the database locale or a runtime's evolving character tables.
    private static bool IsWhitespace(char value) => value is >= '\u0009' and <= '\u000D'
        or '\u0020' or '\u0085' or '\u00A0' or '\u1680'
        or >= '\u2000' and <= '\u200A'
        or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000';

    private static bool IsControl(char value) => value is <= '\u001F' or >= '\u007F' and <= '\u009F';

    public static Guid ValidateId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must not be empty.", parameterName);
        }
        return value;
    }

    public static string ValidateCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        static bool IsAlphanumeric(char c) => c is >= 'A' and <= 'Z' or >= '0' and <= '9';
        if (code.Length is < 1 or > 32 || !IsAlphanumeric(code[0])
            || code.Any(c => !IsAlphanumeric(c) && c is not '-' and not '_'))
        {
            throw new ArgumentException("Code must be 1–32 uppercase ASCII letters, digits, hyphens or underscores, starting with a letter or digit.", nameof(code));
        }
        return code;
    }

    public static string ValidateTimeZoneId(string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(timeZoneId);
        if (timeZoneId.Length is < 1 or > 255 || timeZoneId.Any(c => c > '\u007F' || IsWhitespace(c) || IsControl(c)))
        {
            throw new ArgumentException("Time zone must be an explicit supported IANA identifier.", nameof(timeZoneId));
        }
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            if (!zone.HasIanaId || !string.Equals(zone.Id, timeZoneId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Time zone must use the exact IANA identifier, not a Windows identifier.", nameof(timeZoneId));
            }
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException("IANA time zone is unavailable on this host.", nameof(timeZoneId), exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException("IANA time zone data is invalid on this host.", nameof(timeZoneId), exception);
        }
        return timeZoneId;
    }
}
