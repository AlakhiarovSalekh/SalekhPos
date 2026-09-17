using SalekhPos.Subscriptions.Contracts.Subscriptions;

namespace SalekhPos.Subscriptions.Application.Subscriptions;

public sealed record SubscriptionIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Subscription identity is invalid.");
    }
}
public interface ISubscriptionService
{
    Task<IReadOnlyList<PlanResponse>> ListPlansAsync(SubscriptionIdentity identity, Guid organizationId, CancellationToken cancellationToken);
    Task<SubscriptionResponse> CreateAsync(SubscriptionIdentity identity, Guid organizationId, CreateSubscriptionRequest request, CancellationToken cancellationToken);
    Task<SubscriptionResponse> GetAsync(SubscriptionIdentity identity, Guid organizationId, Guid subscriptionId, CancellationToken cancellationToken);
    Task<SubscriptionResponse> ChangePlanAsync(SubscriptionIdentity identity, Guid organizationId, Guid subscriptionId, ChangePlanRequest request, CancellationToken cancellationToken);
    Task<SubscriptionResponse> CancelAsync(SubscriptionIdentity identity, Guid organizationId, Guid subscriptionId, CancelSubscriptionRequest request, CancellationToken cancellationToken);
    Task<SubscriptionResponse> RenewAsync(SubscriptionIdentity identity, Guid organizationId, Guid subscriptionId, RenewSubscriptionRequest request, CancellationToken cancellationToken);
    Task<EntitlementResponse> GetEntitlementsAsync(SubscriptionIdentity identity, Guid organizationId, Guid subscriptionId, CancellationToken cancellationToken);
}
public sealed class SubscriptionDeniedException : Exception;
public sealed class SubscriptionConflictException(string message) : Exception(message);
public sealed class SubscriptionNotFoundException : Exception;
public sealed class SubscriptionUnavailableException : Exception;
