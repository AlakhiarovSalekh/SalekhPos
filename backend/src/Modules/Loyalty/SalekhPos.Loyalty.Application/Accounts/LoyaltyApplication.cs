using SalekhPos.Loyalty.Contracts.Accounts;

namespace SalekhPos.Loyalty.Application.Accounts;

public sealed record LoyaltyIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Loyalty identity is invalid.");
    }
}

public sealed record OpenLoyaltyAccountCommand(Guid OrganizationId, Guid AccountId, Guid OperationId, Guid CustomerId);
public sealed record ChangeLoyaltyPointsCommand(Guid OrganizationId, Guid AccountId, Guid OperationId,
    int Points, string Reason);

public interface ILoyaltyAccountService
{
    Task<(LoyaltyAccountResponse Account, bool Created)> OpenAsync(LoyaltyIdentity identity,
        OpenLoyaltyAccountCommand command, CancellationToken ct);
    Task<LoyaltyAccountPage> ListAsync(LoyaltyIdentity identity, Guid organizationId,
        int pageSize, Guid? after, CancellationToken ct);
    Task<LoyaltyAccountResponse?> ReadAsync(LoyaltyIdentity identity, Guid organizationId,
        Guid accountId, CancellationToken ct);
    Task<LoyaltyPointsResult> EarnAsync(LoyaltyIdentity identity,
        ChangeLoyaltyPointsCommand command, CancellationToken ct);
    Task<LoyaltyPointsResult> RedeemAsync(LoyaltyIdentity identity,
        ChangeLoyaltyPointsCommand command, CancellationToken ct);
    Task<LoyaltyEventPage> ListEventsAsync(LoyaltyIdentity identity, Guid organizationId,
        Guid accountId, int pageSize, Guid? after, CancellationToken ct);
}

public sealed class LoyaltyDeniedException : Exception;
public sealed class LoyaltyUnavailableException : Exception;
public sealed class LoyaltyConflictException : Exception;
public sealed class LoyaltyNotFoundException : Exception;
public sealed class InsufficientLoyaltyPointsException : Exception;
