using SalekhPos.FeatureManagement.Contracts.Features;

namespace SalekhPos.FeatureManagement.Application.Features;

public sealed record FeatureIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Feature identity is invalid.");
    }
}

public interface IFeaturePolicyService
{
    Task<FeatureDecisionResponse> EvaluateAsync(FeatureIdentity identity, Guid organizationId,
        string key, CancellationToken cancellationToken);
    Task<FeatureOverrideResponse> SetOverrideAsync(FeatureIdentity identity, Guid organizationId,
        string key, bool enabled, string reason, CancellationToken cancellationToken);
}

public sealed class FeatureDeniedException : Exception;
public sealed class FeatureNotFoundException : Exception;
public sealed class FeatureUnavailableException : Exception;
