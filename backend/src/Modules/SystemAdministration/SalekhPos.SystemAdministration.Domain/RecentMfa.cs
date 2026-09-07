namespace SalekhPos.SystemAdministration.Domain;

public static class RecentMfa
{
    // Security policy, also enforced inside privileged database functions.
    public static bool IsRecent(DateTimeOffset authenticatedAt, DateTimeOffset now) =>
        authenticatedAt >= now.AddMinutes(-5) && authenticatedAt <= now.AddSeconds(30);
}
