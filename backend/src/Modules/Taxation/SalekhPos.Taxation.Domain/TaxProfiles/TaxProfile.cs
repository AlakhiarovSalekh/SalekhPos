namespace SalekhPos.Taxation.Domain.TaxProfiles;

public sealed record TaxProfile
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string CountryCode { get; }
    public bool PricesIncludeTax { get; }

    public TaxProfile(Guid organizationId, Guid id, string code, string name, string countryCode, bool pricesIncludeTax)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty) throw new ArgumentException("Tax profile identity is invalid.");
        OrganizationId = organizationId; Id = id;
        Code = Normalize(code, 40, nameof(code)); Name = Text(name, 120, nameof(name));
        countryCode = countryCode?.Trim().ToUpperInvariant() ?? "";
        if (countryCode.Length != 2 || countryCode.Any(c => c is < 'A' or > 'Z')) throw new ArgumentException("Country code is invalid.", nameof(countryCode));
        CountryCode = countryCode; PricesIncludeTax = pricesIncludeTax;
    }
    private static string Normalize(string value, int max, string name) { var s = Text(value, max, name).ToUpperInvariant(); if (s.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw new ArgumentException("Code is invalid.", name); return s; }
    private static string Text(string value, int max, string name) { value = value?.Trim() ?? ""; if (value.Length is < 1 || value.Length > max || value.Any(char.IsControl)) throw new ArgumentException("Text is invalid.", name); return value; }
}
