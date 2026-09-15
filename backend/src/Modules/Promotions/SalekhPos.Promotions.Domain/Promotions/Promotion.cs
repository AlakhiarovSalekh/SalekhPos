namespace SalekhPos.Promotions.Domain.Promotions;

public enum PromotionDiscountKind { Percentage, FixedAmount }

public sealed record Promotion
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public Guid? BranchId { get; }
    public PromotionDiscountKind Kind { get; }
    public decimal Value { get; }
    public string? Currency { get; }
    public decimal MinimumSubtotal { get; }
    public DateTimeOffset StartsAt { get; }
    public DateTimeOffset? EndsAt { get; }
    public bool IsActive { get; }

    public Promotion(Guid organizationId, Guid id, string code, string name, Guid? branchId,
        PromotionDiscountKind kind, decimal value, string? currency, decimal minimumSubtotal,
        DateTimeOffset startsAt, DateTimeOffset? endsAt, bool isActive = true)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Promotion identifiers are invalid.");
        if (startsAt == default || startsAt.Offset != TimeSpan.Zero
            || endsAt.HasValue && (endsAt.Value.Offset != TimeSpan.Zero || endsAt <= startsAt))
            throw new ArgumentException("Promotion window is invalid.");
        OrganizationId = organizationId; Id = id; BranchId = branchId;
        Code = NormalizeCode(code); Name = Required(name, 160, "name"); Kind = kind;
        Currency = NormalizeCurrency(kind, currency); Value = ValidateValue(kind, value);
        MinimumSubtotal = ValidateMoney(minimumSubtotal, "minimum subtotal");
        StartsAt = startsAt; EndsAt = endsAt; IsActive = isActive;
    }
    public decimal DiscountFor(decimal subtotal, string currency, DateTimeOffset at)
    {
        if (!IsActive || at < StartsAt || EndsAt.HasValue && at >= EndsAt.Value || subtotal < MinimumSubtotal)
            return 0m;
        if (subtotal < 0 || decimal.Round(subtotal, 6) != subtotal)
            throw new ArgumentException("Subtotal is invalid.");
        var normalizedCurrency = NormalizeCurrency(PromotionDiscountKind.FixedAmount, currency)!;
        if (Currency is not null && Currency != normalizedCurrency) return 0m;
        var raw = Kind == PromotionDiscountKind.Percentage
            ? subtotal * Value / 100m
            : Value;
        var discount = decimal.Round(raw, 6, MidpointRounding.ToEven);
        return Math.Min(subtotal, discount);
    }

    private static string NormalizeCode(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 1 or > 40
            || !normalized.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            throw new ArgumentException("Promotion code is invalid.");
        return normalized;
    }

    private static string Required(string value, int maximum, string field)
    {
        if (value is null) throw new ArgumentNullException(field);
        var trimmed = value.Trim();
        if (trimmed.Length is < 1 || trimmed.Length > maximum || trimmed.Any(char.IsControl))
            throw new ArgumentException($"Promotion {field} is invalid.");
        return trimmed;
    }
    private static string? NormalizeCurrency(PromotionDiscountKind kind, string? value)
    {
        if (kind == PromotionDiscountKind.Percentage && string.IsNullOrWhiteSpace(value)) return null;
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Currency is required.");
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Promotion currency is invalid.");
        return normalized;
    }

    private static decimal ValidateValue(PromotionDiscountKind kind, decimal value)
    {
        if (value <= 0 || decimal.Round(value, 6) != value)
            throw new ArgumentException("Promotion discount value is invalid.");
        if (kind == PromotionDiscountKind.Percentage && value > 100)
            throw new ArgumentException("Promotion percentage is invalid.");
        return value;
    }
    private static decimal ValidateMoney(decimal value, string field)
    {
        if (value < 0 || decimal.Round(value, 6) != value)
            throw new ArgumentException("Promotion money value is invalid.");
        return value;
    }
}
