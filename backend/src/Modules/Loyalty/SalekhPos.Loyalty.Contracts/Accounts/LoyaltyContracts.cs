namespace SalekhPos.Loyalty.Contracts.Accounts;

public sealed record OpenLoyaltyAccountRequest(Guid CustomerId);
public sealed record LoyaltyAccountResponse(Guid Id, Guid CustomerId, string Tier,
    long PointsBalance, long LifetimePoints, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record LoyaltyAccountPage(IReadOnlyList<LoyaltyAccountResponse> Items, Guid? NextCursor);
public sealed record LoyaltyPointsRequest(int Points, string Reason);
public sealed record LoyaltyPointsResult(LoyaltyAccountResponse Account, Guid EventId, string Kind,
    int Points, DateTimeOffset OccurredAt, bool Applied);
public sealed record LoyaltyEventResponse(Guid Id, string Kind, int PointsDelta, string Reason,
    DateTimeOffset OccurredAt);
public sealed record LoyaltyEventPage(IReadOnlyList<LoyaltyEventResponse> Items, Guid? NextCursor);
