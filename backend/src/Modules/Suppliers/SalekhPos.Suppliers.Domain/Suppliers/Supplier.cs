namespace SalekhPos.Suppliers.Domain.Suppliers;

public sealed class Supplier
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string? TaxId { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public bool IsActive { get; }

    public Supplier(Guid organizationId, Guid id, string code, string name, string? taxId,
        string? email, string? phone, bool isActive = true)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty)
            throw new ArgumentException("Supplier identifiers are required.");
        OrganizationId = organizationId;
        Id = id;
        Code = CodeValue(code);
        Name = Text(name, 180, nameof(name));
        TaxId = OptionalToken(taxId, 64, nameof(taxId));
        Email = EmailValue(email);
        Phone = PhoneValue(phone);
        IsActive = isActive;
    }

    private static string CodeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 40 || value != value.Trim()
            || value.Any(char.IsControl) || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ArgumentException("Supplier code is invalid.", nameof(value));
        return value;
    }

    private static string Text(string value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value != value.Trim()
            || value.Any(char.IsControl)) throw new ArgumentException("Supplier text is invalid.", parameter);
        return value;
    }

    private static string? OptionalToken(string? value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > maximum || value.Any(char.IsControl))
            throw new ArgumentException("Supplier token is invalid.", parameter);
        return value;
    }

    private static string? EmailValue(string? value)
    {
        value = OptionalToken(value, 254, nameof(value));
        if (value is not null && (value.Count(c => c == '@') != 1 || value.StartsWith('@') || value.EndsWith('@')))
            throw new ArgumentException("Supplier email is invalid.", nameof(value));
        return value;
    }

    private static string? PhoneValue(string? value)
    {
        value = OptionalToken(value, 32, nameof(value));
        if (value is not null && value.Any(c => !(char.IsDigit(c) || c is '+' or '-' or ' ' or '(' or ')')))
            throw new ArgumentException("Supplier phone is invalid.", nameof(value));
        return value;
    }
}
