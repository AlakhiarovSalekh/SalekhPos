using System.Net;
using System.Text;
using SalekhPos.Desktop.Infrastructure.Sync;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class CashManagementHttpTests
{
    [Fact]
    public async Task MovementUsesScopedEndpointAndRetryStableOperationId()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var shift = Guid.NewGuid();
        var operation = Guid.NewGuid(); HttpRequestMessage? captured = null;
        using var client = Client(request =>
        {
            captured = request;
            return Json($$"""{"id":"{{Guid.NewGuid():D}}","shiftId":"{{shift:D}}","kind":"cash_out","currency":"GEL","amount":3,"reason":"Petty cash","recordedAt":"{{DateTimeOffset.UtcNow:O}}","recordedBy":"cashier"}""");
        });

        var result = await new HttpRemoteCashManagement(client).RecordMovementAsync(organization, branch, shift,
            operation, "cash_out", 3m, "Petty cash", default);

        Assert.Equal(3m, result.Amount); Assert.Equal(operation.ToString("D"),
            captured!.Headers.GetValues("Idempotency-Key").Single());
        Assert.EndsWith($"/shifts/{shift:D}/cash-movements", captured.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CloseRejectsMismatchedServerEvidence()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var shift = Guid.NewGuid();
        using var client = Client(_ => Json($$"""{"id":"{{shift:D}}","branchId":"{{branch:D}}","registerId":"{{Guid.NewGuid():D}}","status":"closed","currency":"GEL","openingBalance":0,"cashSales":0,"cashRefunds":0,"cashIn":0,"cashOut":0,"expectedCash":0,"countedCash":99,"variance":99,"openedAt":"{{DateTimeOffset.UtcNow.AddHours(-1):O}}","closedAt":"{{DateTimeOffset.UtcNow:O}}","openedBy":"cashier","closedBy":"cashier"}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpRemoteCashManagement(client).CloseAsync(
            organization, branch, shift, Guid.NewGuid(), 10m, default));
    }

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new(new Handler(response))
    { BaseAddress = new("https://pos.test/") };
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
