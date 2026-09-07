using SalekhPos.Identity.Application;
using Xunit;

namespace SalekhPos.Tests;

public sealed class AuthenticatedCredentialTests
{
    [Fact]
    public void FingerprintIsStableAndDoesNotRetainTheBearerToken()
    {
        var first = new AuthenticatedCredential("https://issuer.test", "alice", "header.payload.signature", DateTime.UtcNow);
        var same = new AuthenticatedCredential("https://issuer.test", "alice", "header.payload.alternate-signature", DateTime.UtcNow);
        var other = new AuthenticatedCredential("https://issuer.test", "alice", "header.other-payload.signature", DateTime.UtcNow);
        Assert.Equal(first.Fingerprint, same.Fingerprint);
        Assert.NotEqual(first.Fingerprint, other.Fingerprint);
        Assert.Matches("^[A-F0-9]{64}$", first.Fingerprint);
        Assert.DoesNotContain("header.payload", first.ToString());
    }

    [Theory]
    [InlineData("", "alice", "token")]
    [InlineData("issuer", "", "token")]
    [InlineData("issuer", "alice", "")]
    [InlineData("issuer\n", "alice", "token")]
    [InlineData("issuer", "alice\n", "token")]
    public void InvalidIdentityIsRejected(string issuer, string subject, string token) =>
        Assert.Throws<ArgumentException>(() => new AuthenticatedCredential(issuer, subject, token, DateTime.UtcNow));

    [Fact]
    public void UnspecifiedExpiryIsRejected() => Assert.Throws<ArgumentException>(() =>
        new AuthenticatedCredential("issuer", "alice", "token", new DateTime(2026, 9, 7)));
}
