namespace SalekhPos.Billing.Domain.Accounts;

public sealed record BillingAccount
{
    public BillingAccount(Guid id, Guid organizationId, string legalName, string billingEmail,
        string currency, string? taxIdentifier, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty) throw new ArgumentException("Billing account identifiers are required.");
        LegalName = Required(legalName, 160, "Legal name");
        BillingEmail = Required(billingEmail, 320, "Billing email").ToLowerInvariant();
        if (!BillingEmail.Contains('@', StringComparison.Ordinal)) throw new ArgumentException("Billing email is invalid.");
        Currency = CurrencyCode.Normalize(currency);
        TaxIdentifier = string.IsNullOrWhiteSpace(taxIdentifier) ? null : Required(taxIdentifier, 80, "Tax identifier");
        if (createdAt == default || createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Created timestamp must be UTC.");
        Id = id; OrganizationId = organizationId; CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public string LegalName { get; }
    public string BillingEmail { get; }
    public string Currency { get; }
    public string? TaxIdentifier { get; }
    public DateTimeOffset CreatedAt { get; }

    public static string Required(string value, int maximum, string name)
    {
        value = value.Trim();
        if (value.Length is 0 || value.Length > maximum || value.Any(char.IsControl))
            throw new ArgumentException($"{name} is invalid.");
        return value;
    }
}

public static class CurrencyCode
{
    public static string Normalize(string value)
    {
        value = value.Trim().ToUpperInvariant();
        if (value.Length != 3 || value.Any(c => c is < 'A' or > 'Z')) throw new ArgumentException("Currency is invalid.");
        return value;
    }
}
