using SalekhPos.Promotions.Contracts.Campaigns;
using SalekhPos.Promotions.Domain.Promotions;

namespace SalekhPos.Promotions.Application.Campaigns;

public sealed record PromotionIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Promotion identity is invalid.");
    }
}

public sealed record CreatePromotionCommand(Guid OrganizationId, Guid PromotionId, Guid OperationId,
    string Code, string Name, Guid? BranchId, string DiscountKind, decimal Value, string? Currency,
    decimal MinimumSubtotal, DateTimeOffset StartsAt, DateTimeOffset? EndsAt)
{
    public Promotion ToPromotion() => new(OrganizationId, PromotionId, Code, Name, BranchId,
        DiscountKind == "percentage" ? PromotionDiscountKind.Percentage : DiscountKind == "fixed"
            ? PromotionDiscountKind.FixedAmount : throw new ArgumentException("Promotion kind is invalid."),
        Value, Currency, MinimumSubtotal, StartsAt, EndsAt);
}

public sealed record PromotionWriteResult(PromotionResponse Promotion, bool Created);
public interface IPromotionService
{
    Task<PromotionWriteResult> CreateAsync(PromotionIdentity identity, CreatePromotionCommand command, CancellationToken ct);
    Task<PromotionPage> ListAsync(PromotionIdentity identity, Guid organizationId, Guid branchId, int pageSize, Guid? after, CancellationToken ct);
    Task<PromotionResponse?> ReadAsync(PromotionIdentity identity, Guid organizationId, Guid branchId, Guid promotionId, CancellationToken ct);
    Task<PromotionResponse> DeactivateAsync(PromotionIdentity identity, Guid organizationId, Guid branchId,
        Guid promotionId, long expectedVersion, CancellationToken ct);
    Task<PromotionEvaluationResponse> EvaluateAsync(PromotionIdentity identity, Guid organizationId, Guid branchId,
        decimal subtotal, string currency, DateTimeOffset at, CancellationToken ct);
}

public sealed class PromotionsDeniedException : Exception;
public sealed class PromotionsUnavailableException : Exception;
public sealed class PromotionConflictException : Exception;
public sealed class PromotionNotFoundException : Exception;
