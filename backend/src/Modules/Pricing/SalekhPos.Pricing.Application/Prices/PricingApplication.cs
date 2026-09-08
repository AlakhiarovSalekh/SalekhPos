using SalekhPos.Pricing.Contracts.Prices;
using SalekhPos.Pricing.Domain.Prices;

namespace SalekhPos.Pricing.Application.Prices;

public sealed record PricingIdentity
{
    public string Issuer { get; }
    public string Subject { get; }
    public PricingIdentity(string issuer, string subject)
    {
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 2048 || string.IsNullOrWhiteSpace(subject)
            || subject.Length > 256 || issuer.Any(char.IsControl) || subject.Any(char.IsControl))
            throw new ArgumentException("Identity is invalid.");
        Issuer = issuer;
        Subject = subject;
    }
}
public sealed record SchedulePriceCommand(Guid OrganizationId, Guid PriceId, Guid OperationId, Guid ProductId,
    Guid? BranchId, decimal Amount, string Currency, TaxMode TaxMode, decimal TaxRate,
    DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil)
{
    public PriceEntry ToEntry() => new(OrganizationId, PriceId, ProductId, BranchId, Amount, Currency,
        TaxMode, TaxRate, ValidFrom, ValidUntil);
}
public sealed record PriceWriteResult(PriceResponse Price, bool Created);
public interface IPriceBook
{
    Task<PriceWriteResult> ScheduleAsync(PricingIdentity identity, SchedulePriceCommand command, CancellationToken cancellationToken);
    Task<ResolvedPriceResponse?> ResolveAsync(PricingIdentity identity, Guid organizationId, Guid branchId,
        Guid productId, DateTimeOffset at, CancellationToken cancellationToken);
}
public sealed class PricingDeniedException : Exception;
public sealed class PricingConflictException : Exception;
public sealed class PricingUnavailableException : Exception;
