namespace SalekhPos.Desktop.Application.Management;

public sealed record TransferLineSummary(Guid ProductId, decimal Quantity);
public sealed record StockTransferSummary(Guid Id, Guid SourceBranchId, Guid DestinationBranchId,
    string Status, string? Reference, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateTimeOffset? DispatchedAt, DateTimeOffset? ReceivedAt, IReadOnlyList<TransferLineSummary> Lines);
public sealed record CreateStockTransferInput(Guid DestinationBranchId, string? Reference,
    IReadOnlyList<TransferLineSummary> Lines);
public sealed record PromotionSummary(Guid Id, string Code, string Name, Guid? BranchId,
    string DiscountKind, decimal Value, string? Currency, decimal MinimumSubtotal,
    DateTimeOffset StartsAt, DateTimeOffset? EndsAt, bool IsActive, long Version,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record CreatePromotionInput(string Code, string Name, string DiscountKind,
    decimal Value, string? Currency, decimal MinimumSubtotal, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);
public sealed record AppliedPromotionSummary(Guid PromotionId, string Code, string Name, decimal Discount);
public sealed record PromotionEvaluationSummary(decimal Subtotal, decimal TotalDiscount, decimal Payable,
    string Currency, IReadOnlyList<AppliedPromotionSummary> Applied);
public sealed record LoyaltyAccountSummary(Guid Id, Guid CustomerId, string Tier, long PointsBalance,
    long LifetimePoints, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record LoyaltyPointsSummary(LoyaltyAccountSummary Account, Guid EventId, string Kind,
    int Points, DateTimeOffset OccurredAt, bool Applied);
public interface ICommerceExtensions
{
    Task<UuidPage<StockTransferSummary>> ListStockTransfersAsync(Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<StockTransferSummary> CreateStockTransferAsync(Guid organizationId, Guid branchId,
        CreateStockTransferInput input, Guid operationId, CancellationToken cancellationToken);
    Task<StockTransferSummary> ChangeStockTransferAsync(Guid organizationId, Guid branchId,
        StockTransferSummary transfer, string action, CancellationToken cancellationToken);
    Task<UuidPage<PromotionSummary>> ListPromotionsAsync(Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<PromotionSummary> CreatePromotionAsync(Guid organizationId, Guid branchId,
        CreatePromotionInput input, Guid operationId, CancellationToken cancellationToken);
    Task<PromotionSummary> DeactivatePromotionAsync(Guid organizationId, Guid branchId,
        PromotionSummary promotion, CancellationToken cancellationToken);
    Task<PromotionEvaluationSummary> EvaluatePromotionsAsync(Guid organizationId, Guid branchId,
        decimal subtotal, string currency, DateTimeOffset at, CancellationToken cancellationToken);
    Task<UuidPage<LoyaltyAccountSummary>> ListLoyaltyAccountsAsync(Guid organizationId,
        int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<LoyaltyAccountSummary> OpenLoyaltyAccountAsync(Guid organizationId, Guid customerId,
        Guid operationId, CancellationToken cancellationToken);
    Task<LoyaltyPointsSummary> ChangeLoyaltyPointsAsync(Guid organizationId, LoyaltyAccountSummary account,
        string action, int points, string reason, Guid operationId, CancellationToken cancellationToken);
}
