using SalekhPos.Analytics.Contracts.Analytics;
using SalekhPos.Analytics.Domain.Analytics;

namespace SalekhPos.Analytics.Application.Analytics;

public sealed record AnalyticsIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Analytics identity is invalid.");
    }
}

public interface IAnalyticsReader
{
    Task<AnalyticsOverviewResponse> ReadOverviewAsync(AnalyticsIdentity identity,
        Guid organizationId, Guid branchId, AnalyticsWindow window, CancellationToken cancellationToken);
    Task<SalesTrendResponse> ReadSalesTrendAsync(AnalyticsIdentity identity,
        Guid organizationId, Guid branchId, AnalyticsWindow window, CancellationToken cancellationToken);
    Task<StoreComparisonResponse> CompareStoresAsync(AnalyticsIdentity identity,
        Guid organizationId, AnalyticsWindow window, CancellationToken cancellationToken);
}

public sealed class AnalyticsDeniedException : Exception;
public sealed class AnalyticsUnavailableException : Exception;
