namespace SalekhPos.Employees.Domain.Employees;

public sealed record Employee
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public string Code { get; }
    public string DisplayName { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public string JobTitle { get; }
    public bool IsActive { get; }

    public Employee(Guid organizationId, Guid id, string code, string displayName,
        string? email, string? phone, string jobTitle, bool isActive = true)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty) throw new ArgumentException("Employee identifiers are required.");
        OrganizationId = organizationId; Id = id;
        Code = NormalizeCode(code); DisplayName = Required(displayName, 160, "display name");
        Email = OptionalEmail(email); Phone = Optional(phone, 40, "phone");
        JobTitle = Required(jobTitle, 100, "job title"); IsActive = isActive;
    }

    private static string NormalizeCode(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 1 or > 32 || !normalized.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            throw new ArgumentException("Employee code is invalid.");
        return normalized;
    }

    private static string Required(string value, int maximum, string field)
    {
        if (value is null) throw new ArgumentNullException(field);
        var trimmed = value.Trim();
        if (trimmed.Length is < 1 || trimmed.Length > maximum || trimmed.Any(char.IsControl))
            throw new ArgumentException($"Employee {field} is invalid.");
        return trimmed;
    }

    private static string? Optional(string? value, int maximum, string field)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        if (trimmed.Length is < 1 || trimmed.Length > maximum || trimmed.Any(char.IsControl))
            throw new ArgumentException($"Employee {field} is invalid.");
        return trimmed;
    }

    private static string? OptionalEmail(string? value)
    {
        var email = Optional(value, 254, "email");
        if (email is null) return null;
        var at = email.IndexOf('@');
        if (at < 1 || at != email.LastIndexOf('@') || at == email.Length - 1 || email.Contains(' '))
            throw new ArgumentException("Employee email is invalid.");
        return email.ToLowerInvariant();
    }
}
