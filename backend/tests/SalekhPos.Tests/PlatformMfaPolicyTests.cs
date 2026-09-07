using System.Globalization;
using System.Security.Claims;
using SalekhPos.SystemAdministration.Api;
using SalekhPos.SystemAdministration.Application;
using SalekhPos.SystemAdministration.Domain;
using Xunit;

namespace SalekhPos.Tests;

public sealed class PlatformMfaPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(
        new[] { new Claim("iss", "https://identity.test"), new Claim("sub", "root") }.Concat(claims), "Bearer"));

    [Theory]
    [InlineData(-300, true)]
    [InlineData(-301, false)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void OnlyExplicitRecentMfaIsAccepted(int offset, bool allowed)
    {
        var policy = new PlatformMfaPolicy("required-mfa", new FixedClock());
        var principal = Principal(new Claim("acr", "required-mfa"),
            new Claim("auth_time", Now.AddSeconds(offset).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        if (allowed) { Assert.True(policy.RequireActor(principal).HasMfa); }
        else { Assert.Throws<PlatformAccessDeniedException>(() => policy.RequireActor(principal)); }
    }

    [Theory]
    [InlineData("missing-acr")]
    [InlineData("wrong-acr")]
    [InlineData("duplicate-acr")]
    [InlineData("duplicate-time")]
    [InlineData("invalid-time")]
    [InlineData("overflow-time")]
    [InlineData("no-configuration")]
    public void AmbiguousOrUnprovenAssuranceCannotElevate(string scenario)
    {
        var claims = new List<Claim> { new("role", "RootSuperAdmin"), new("amr", "mfa") };
        if (scenario != "missing-acr") { claims.Add(new Claim("acr", scenario == "wrong-acr" ? "other" : "required-mfa")); }
        if (scenario == "duplicate-acr") { claims.Add(new Claim("acr", "required-mfa")); }
        claims.Add(new Claim("auth_time", scenario switch
        {
            "invalid-time" => "not-a-timestamp",
            "overflow-time" => "999999999999999999999",
            _ => Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
        }));
        if (scenario == "duplicate-time") { claims.Add(new Claim("auth_time", "1")); }
        var policy = new PlatformMfaPolicy(scenario == "no-configuration" ? null : "required-mfa", new FixedClock());
        Assert.Throws<PlatformAccessDeniedException>(() => policy.RequireActor(Principal([.. claims])));
    }

    [Theory]
    [InlineData("http://identity.test", "root")]
    [InlineData("https://user@identity.test", "root")]
    [InlineData("https://identity.test?query", "root")]
    [InlineData("https://identity.test", "")]
    [InlineData("https://identity.test", "root\n")]
    public void InvalidPlatformIdentityIsRejected(string issuer, string subject) =>
        Assert.Throws<ArgumentException>(() => new PlatformIdentity(issuer, subject));
}
