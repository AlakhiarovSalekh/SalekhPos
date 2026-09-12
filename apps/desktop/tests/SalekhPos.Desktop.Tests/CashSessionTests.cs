using System.Net;
using System.Text;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Infrastructure.LocalDatabase;
using SalekhPos.Desktop.Infrastructure.Sync;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class CashSessionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "salekhpos-session-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RemoteAssignmentAndOpenShiftArePersistedAndSurviveReopen()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid(); var opened = DateTimeOffset.UtcNow.AddHours(-1);
        using var client = Client(device, branch, register, shift, opened);
        var path = Path.Combine(directory, "session.db"); var first = new SqliteCashSessionStore(path);
        var coordinator = new CashSessionCoordinator(new HttpRemoteCashSessionSource(client), first);

        var result = await coordinator.RefreshAsync(organization, branch, device, default);
        var reopened = await new SqliteCashSessionStore(path).ReadActiveAsync(organization, branch, device, default);

        Assert.NotNull(result); Assert.Equal(shift, result.ShiftId); Assert.Equal(register, result.RegisterId);
        Assert.Equal("GEL", result.Currency); Assert.Equal(result, reopened);
    }

    [Fact]
    public async Task ConfirmedMissingOpenShiftSupersedesCachedSession()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid(); var path = Path.Combine(directory, "closed.db");
        var store = new SqliteCashSessionStore(path); var captured = DateTimeOffset.UtcNow;
        await store.ApplyAsync(organization, branch, device, new(new(device, branch, register, "active", 1),
            new(shift, branch, register, "open", "GEL", 5m, captured.AddHours(-1)), captured), default);

        var result = await store.ApplyAsync(organization, branch, device,
            new(new(device, branch, register, "active", 1), null, captured.AddMinutes(1)), default);

        Assert.Null(result); Assert.Null(await store.ReadActiveAsync(organization, branch, device, default));
    }

    [Fact]
    public async Task ChangedSameShiftEvidenceIsRejected()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid(); var path = Path.Combine(directory, "replay.db");
        var store = new SqliteCashSessionStore(path); var captured = DateTimeOffset.UtcNow;
        var snapshot = new RemoteCashSessionSnapshot(new(device, branch, register, "active", 1),
            new(shift, branch, register, "open", "GEL", 5m, captured.AddHours(-1)), captured);
        await store.ApplyAsync(organization, branch, device, snapshot, default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ApplyAsync(organization, branch, device,
            snapshot with { Shift = snapshot.Shift! with { OpeningBalance = 6m }, CapturedAt = captured.AddMinutes(1) }, default));
    }

    private static HttpClient Client(Guid device, Guid branch, Guid register, Guid shift, DateTimeOffset opened)
    {
        var calls = 0;
        return new HttpClient(new Handler(_ =>
        {
            calls++;
            var json = calls == 1
                ? $$"""{"id":"{{device:D}}","branchId":"{{branch:D}}","registerId":"{{register:D}}","status":"active","syncProtocolVersion":1}"""
                : $$"""{"id":"{{shift:D}}","branchId":"{{branch:D}}","registerId":"{{register:D}}","status":"open","currency":"GEL","openingBalance":5,"openedAt":"{{opened:O}}"}""";
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }))
        { BaseAddress = new("https://pos.test/") };
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
