namespace SalekhPos.Subscriptions.Domain.Entitlements;

public sealed record EntitlementSnapshot
{
    public EntitlementSnapshot(Guid subscriptionId, IReadOnlyDictionary<string, long> limits, DateTimeOffset validUntil)
    {
        if (subscriptionId == Guid.Empty) throw new ArgumentException("Subscription identifier is required.");
        if (validUntil == default || validUntil.Offset != TimeSpan.Zero) throw new ArgumentException("Validity timestamp must be UTC.");
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var item in limits)
        {
            var key = Plans.SubscriptionPlan.Required(item.Key, 80, "Entitlement key").ToLowerInvariant();
            if (item.Value < -1 || !values.TryAdd(key, item.Value)) throw new ArgumentException("Entitlements are invalid.");
        }
        SubscriptionId = subscriptionId; Limits = values; ValidUntil = validUntil;
    }
    public Guid SubscriptionId { get; }
    public IReadOnlyDictionary<string, long> Limits { get; }
    public DateTimeOffset ValidUntil { get; }
    public bool Allows(string key, long usage, DateTimeOffset at)
    {
        if (usage < 0 || at.Offset != TimeSpan.Zero) return false;
        return at < ValidUntil && Limits.TryGetValue(key.Trim().ToLowerInvariant(), out var limit) && (limit == -1 || usage < limit);
    }
}
