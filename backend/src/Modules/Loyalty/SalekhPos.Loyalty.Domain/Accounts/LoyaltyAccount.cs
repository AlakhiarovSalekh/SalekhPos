namespace SalekhPos.Loyalty.Domain.Accounts;

public enum LoyaltyTier { Bronze, Silver, Gold, Platinum }

public sealed record LoyaltyAccount
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public long PointsBalance { get; }
    public long LifetimePoints { get; }
    public LoyaltyTier Tier { get; }

    public LoyaltyAccount(Guid organizationId, Guid id, Guid customerId,
        long pointsBalance = 0, long lifetimePoints = 0)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty || customerId == Guid.Empty)
            throw new ArgumentException("Loyalty identifiers are invalid.");
        if (pointsBalance < 0 || lifetimePoints < 0 || pointsBalance > lifetimePoints)
            throw new ArgumentException("Loyalty balances are invalid.");
        OrganizationId = organizationId; Id = id; CustomerId = customerId;
        PointsBalance = pointsBalance; LifetimePoints = lifetimePoints;
        Tier = TierFor(lifetimePoints);
    }

    public static LoyaltyTier TierFor(long lifetimePoints) => lifetimePoints switch
    {
        >= 10000 => LoyaltyTier.Platinum,
        >= 5000 => LoyaltyTier.Gold,
        >= 1000 => LoyaltyTier.Silver,
        _ => LoyaltyTier.Bronze
    };

    public static int ValidatePoints(int points)
    {
        if (points is < 1 or > 1_000_000) throw new ArgumentException("Loyalty points are invalid.");
        return points;
    }
}
