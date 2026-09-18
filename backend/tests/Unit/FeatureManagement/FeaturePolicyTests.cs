using SalekhPos.FeatureManagement.Domain.Features;

namespace SalekhPos.Tests.FeatureManagement;

public sealed class FeaturePolicyTests
{
    [Fact]
    public void EmergencyDisableAlwaysWins() =>
        Assert.False(new FeaturePolicy("sales.beta", true, true, 100)
            .Evaluate(Guid.NewGuid(), true, true));

    [Fact]
    public void TenantEnableCannotBypassEntitlement() =>
        Assert.False(new FeaturePolicy("reports.export", false, false, 0)
            .Evaluate(Guid.NewGuid(), true, false));

    [Fact]
    public void RolloutIsStableForTenant()
    {
        var organizationId = Guid.NewGuid();
        var policy = new FeaturePolicy("inventory.next", false, false, 37);
        Assert.Equal(policy.Evaluate(organizationId, null, true),
            policy.Evaluate(organizationId, null, true));
    }
}
