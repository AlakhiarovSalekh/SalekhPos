namespace SalekhPos.Catalog.Domain.Products;

public sealed record Product
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public string Sku { get; }
    public string Name { get; }
    public string UnitCode { get; }
    public string? Barcode { get; }
    public bool IsActive { get; }

    public Product(Guid organizationId, Guid id, string sku, string name, string unitCode,
        string? barcode = null, bool isActive = true)
    {
        OrganizationId = RequiredId(organizationId, nameof(organizationId));
        Id = RequiredId(id, nameof(id));
        Sku = Code(sku, nameof(sku), 64, allowDot: true);
        Name = Text(name, nameof(name), 200);
        UnitCode = Code(unitCode, nameof(unitCode), 16, allowDot: false);
        Barcode = OptionalBarcode(barcode);
        IsActive = isActive;
    }

    public Product Rename(string name) => new(OrganizationId, Id, Sku, name, UnitCode, Barcode, IsActive);
    public Product Deactivate() => new(OrganizationId, Id, Sku, Name, UnitCode, Barcode, false);

    private static Guid RequiredId(Guid value, string parameter) => value == Guid.Empty
        ? throw new ArgumentException("ID must not be empty.", parameter) : value;

    private static string Text(string value, string parameter, int maximum)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length is < 1 || value.Length > maximum || value != value.Trim()
            || value.Any(char.IsControl) || value.Any(char.IsSurrogate))
        {
            throw new ArgumentException("Text is invalid.", parameter);
        }
        return value;
    }

    private static string Code(string value, string parameter, int maximum, bool allowDot)
    {
        var code = Text(value, parameter, maximum);
        if (code.Any(character => !(character is >= 'A' and <= 'Z' or >= '0' and <= '9'
                or '_' or '-' || allowDot && character == '.')))
        {
            throw new ArgumentException("Code is invalid.", parameter);
        }
        return code;
    }

    private static string? OptionalBarcode(string? value)
    {
        if (value is null) return null;
        if (value.Length is < 4 or > 64 || value != value.Trim() || value.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new ArgumentException("Barcode is invalid.", nameof(value));
        }
        return value;
    }
}
