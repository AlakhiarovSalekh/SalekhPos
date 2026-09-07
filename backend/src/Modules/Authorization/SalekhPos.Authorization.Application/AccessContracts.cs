namespace SalekhPos.Authorization.Application;

// Only the authentication boundary constructs this identity from a validated token.
// Tenant, role and permission claims supplied by clients are never used here.
public sealed record AccessIdentity
{
    public string Issuer { get; }
    public string Subject { get; }

    public AccessIdentity(string issuer, string subject)
    {
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 2048 || issuer.Any(char.IsControl))
        {
            throw new ArgumentException("Invalid identity issuer.", nameof(issuer));
        }
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 256 || subject.Any(char.IsControl))
        {
            throw new ArgumentException("Invalid identity subject.", nameof(subject));
        }
        Issuer = issuer;
        Subject = subject;
    }
}

public sealed record BranchSummary(Guid Id, Guid BusinessId, Guid? RegionId, string Code,
    string Name, string TimeZoneId);

public sealed record BranchPage(IReadOnlyList<BranchSummary> Items, Guid? NextCursor);

public sealed class AccessDeniedException : Exception;
public sealed class AccessUnavailableException : Exception;
