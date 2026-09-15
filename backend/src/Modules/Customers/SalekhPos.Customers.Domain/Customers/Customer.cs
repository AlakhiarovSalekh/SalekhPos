namespace SalekhPos.Customers.Domain.Customers;

public sealed class Customer
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public string Code { get; }
    public string DisplayName { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public bool IsActive { get; }

    public Customer(Guid organizationId, Guid id, string code, string displayName,
        string? email, string? phone, bool isActive = true)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty)
            throw new ArgumentException("Customer identifiers are required.");
        OrganizationId = organizationId;
        Id = id;
        Code = ValidateCode(code);
        DisplayName = ValidateText(displayName, 160, nameof(displayName));
        Email = ValidateOptionalEmail(email);
        Phone = ValidateOptionalPhone(phone);
        IsActive = isActive;
    }

    private static string ValidateCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 40 || value != value.Trim()
            || value.Any(char.IsControl) || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ArgumentException("Customer code is invalid.", nameof(value));
        return value;
    }

    private static string ValidateText(string value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value != value.Trim()
            || value.Any(char.IsControl))
            throw new ArgumentException("Customer text is invalid.", parameter);
        return value;
    }

    private static string? ValidateOptionalEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 254 || value.Any(char.IsControl) || value.Count(c => c == '@') != 1
            || value.StartsWith('@') || value.EndsWith('@'))
            throw new ArgumentException("Customer email is invalid.", nameof(value));
        return value;
    }

    private static string? ValidateOptionalPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 32 || value.Any(char.IsControl)
            || value.Any(c => !(char.IsDigit(c) || c is '+' or '-' or ' ' or '(' or ')')))
            throw new ArgumentException("Customer phone is invalid.", nameof(value));
        return value;
    }
}
