using Microsoft.Extensions.DependencyInjection;
using SalekhPos.Audit.Application.AuditTrail;
using SalekhPos.Audit.Domain.AuditEvents;
using Xunit;

namespace SalekhPos.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuditTrailTests(AccessFixture fixture)
{
    [Fact]
    public async Task AppendUsesUtcDatabaseTimeAndPersistsImmutableEvidence()
    {
        var audit = fixture.Factory.Services.GetRequiredService<IAuditTrail>();
        var eventId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var draft = new AuditEventDraft(
            fixture.OrganizationA,
            eventId,
            operationId,
            "POST /integration/audit",
            "integration_test",
            null,
            fixture.BranchA,
            null,
            "127.0.0.1",
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            AuditOutcome.Succeeded,
            null);

        var result = await audit.AppendAsync(
            new AuditIdentity(AccessFixture.Issuer, "owner"),
            new AppendAuditCommand(draft),
            CancellationToken.None);

        Assert.True(result.Created);
        Assert.Equal(eventId, result.Event.Id);
        Assert.Equal("succeeded", result.Event.Outcome);
        Assert.Equal(TimeSpan.Zero, result.Event.OccurredAt.Offset);
        Assert.Equal(1L, await fixture.ScalarAsync<long>("""
            SELECT count(*) FROM audit.events
            WHERE organization_id=$1 AND event_id=$2 AND operation_id=$3
            """, fixture.OrganizationA, eventId, operationId));
    }
}
