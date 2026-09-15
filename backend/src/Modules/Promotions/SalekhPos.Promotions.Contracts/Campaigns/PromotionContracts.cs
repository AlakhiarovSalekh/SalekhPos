namespace SalekhPos.Promotions.Contracts.Campaigns;

public sealed record CreatePromotionRequest(string Code, string Name, Guid? BranchId, string DiscountKind,
    decimal Value, string? Currency, decimal MinimumSubtotal, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);
public sealed record PromotionResponse(Guid Id, string Code, string Name, Guid? BranchId, string DiscountKind,
    decimal Value, string? Currency, decimal MinimumSubtotal, DateTimeOffset StartsAt, DateTimeOffset? EndsAt,
    bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record PromotionPage(IReadOnlyList<PromotionResponse> Items, Guid? NextCursor);
public sealed record EvaluatePromotionRequest(decimal Subtotal, string Currency, DateTimeOffset At);
public sealed record AppliedPromotionResponse(Guid PromotionId, string Code, string Name, decimal Discount);
public sealed record PromotionEvaluationResponse(decimal Subtotal, decimal TotalDiscount, decimal Payable,
    string Currency, IReadOnlyList<AppliedPromotionResponse> Applied);
public sealed record ChangePromotionStatusRequest(long ExpectedVersion);
