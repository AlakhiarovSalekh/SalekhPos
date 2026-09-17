namespace SalekhPos.Subscriptions.Domain.Plans;

public enum BillingInterval { Monthly, Annual }

public sealed record SubscriptionPlan
{
    public SubscriptionPlan(Guid id, string code, string name, decimal price, string currency,
        BillingInterval interval, IReadOnlyDictionary<string, long> entitlements, bool active = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("Plan identifier is required.");
        Code = Required(code, 40, "Plan code").ToLowerInvariant();
        if (Code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw new ArgumentException("Plan code is invalid.");
        Name = Required(name, 120, "Plan name");
        if (price < 0 || price > 999_999_999.99m || decimal.Round(price, 2) != price) throw new ArgumentOutOfRangeException(nameof(price));
        Currency = currency.Trim().ToUpperInvariant();
        if (Currency.Length != 3 || Currency.Any(c => c is < 'A' or > 'Z')) throw new ArgumentException("Currency is invalid.");
        if (!Enum.IsDefined(interval)) throw new ArgumentOutOfRangeException(nameof(interval));
        var normalized = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var item in entitlements)
        {
            var key = Required(item.Key, 80, "Entitlement key").ToLowerInvariant();
            if (item.Value < -1) throw new ArgumentOutOfRangeException(nameof(entitlements));
            if (!normalized.TryAdd(key, item.Value)) throw new ArgumentException("Entitlement keys must be unique.");
        }
        Id = id; Price = price; Interval = interval; Entitlements = normalized; Active = active;
    }
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public decimal Price { get; }
    public string Currency { get; }
    public BillingInterval Interval { get; }
    public IReadOnlyDictionary<string, long> Entitlements { get; }
    public bool Active { get; }

    internal static string Required(string value, int maximum, string name)
    {
        value = value.Trim();
        if (value.Length is 0 || value.Length > maximum || value.Any(char.IsControl)) throw new ArgumentException($"{name} is invalid.");
        return value;
    }
}
