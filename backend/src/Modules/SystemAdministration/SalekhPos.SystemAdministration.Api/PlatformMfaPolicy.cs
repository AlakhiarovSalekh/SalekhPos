using System.Globalization;
using System.Security.Claims;
using SalekhPos.SystemAdministration.Application;
using SalekhPos.SystemAdministration.Domain;

namespace SalekhPos.SystemAdministration.Api;

public sealed class PlatformMfaPolicy
{
    private readonly string? requiredAcr;
    private readonly TimeProvider timeProvider;

    public PlatformMfaPolicy(string? requiredAcr, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(requiredAcr)) { requiredAcr = null; }
        if (requiredAcr is not null) { PlatformInput.Text(requiredAcr, 256); }
        this.requiredAcr = requiredAcr;
        this.timeProvider = timeProvider;
    }

    public static PlatformIdentity Identity(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) { throw new PlatformAccessDeniedException(); }
        var issuers = principal.FindAll("iss").ToArray();
        var subjects = principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1) { throw new PlatformAccessDeniedException(); }
        return new PlatformIdentity(issuers[0].Value, subjects[0].Value);
    }

    public PrivilegedActor RequireActor(ClaimsPrincipal principal)
    {
        var identity = Identity(principal);
        var assurance = principal.FindAll("acr").ToArray();
        var timestamps = principal.FindAll("auth_time").ToArray();
        if (requiredAcr is null || assurance.Length != 1 || assurance[0].Value != requiredAcr
            || timestamps.Length != 1 || !long.TryParse(timestamps[0].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var seconds) || seconds < 0 || seconds > 253402300799)
        {
            throw new PlatformAccessDeniedException();
        }
        var authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
        if (!RecentMfa.IsRecent(authenticatedAt, timeProvider.GetUtcNow())) { throw new PlatformAccessDeniedException(); }
        return new PrivilegedActor(identity, authenticatedAt, true);
    }
}
