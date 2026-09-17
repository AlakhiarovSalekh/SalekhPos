using System.Security.Cryptography;
using System.Text;

namespace SalekhPos.FeatureManagement.Domain.Features;

public sealed record FeaturePolicy(string Key, bool DefaultEnabled, bool EmergencyDisabled,
    int RolloutPercentage)
{
    public FeaturePolicy Validate()
    {
        if (string.IsNullOrWhiteSpace(Key) || Key.Length > 100
            || Key.Any(c => !(char.IsLower(c) || char.IsDigit(c) || c is '.' or '-' or '_')))
            throw new ArgumentException("Feature key is invalid.");
        if (RolloutPercentage is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(RolloutPercentage));
        return this;
    }

    public bool Evaluate(Guid organizationId, bool? tenantOverride, bool entitled)
    {
        Validate();
        if (EmergencyDisabled) return false;
        if (tenantOverride.HasValue) return tenantOverride.Value && entitled;
        if (!entitled) return false;
        if (DefaultEnabled || RolloutPercentage == 100) return true;
        if (RolloutPercentage == 0) return false;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{Key}:{organizationId:D}"));
        var bucket = ((bytes[0] << 8) | bytes[1]) % 100;
        return bucket < RolloutPercentage;
    }
}
