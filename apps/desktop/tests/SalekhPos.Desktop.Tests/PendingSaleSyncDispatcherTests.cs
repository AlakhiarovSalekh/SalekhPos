using System.Net;
using System.Text.Json;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Infrastructure.Sync;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class PendingSaleSyncDispatcherTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DispatchesInOrderAndPersistsOnlyMatchingAcknowledgements()
    {
        var deviceId = Guid.NewGuid();
        var messages = new[] { Message(deviceId, 1), Message(deviceId, 2) };
        var store = new Store(messages);
        var dispatcher = new PendingSaleSyncDispatcher(store, new Transport(message => Ack(message)));

        Assert.Equal(2, await dispatcher.DispatchAsync(Guid.NewGuid(), Guid.NewGuid(), deviceId, 10, default));
        Assert.Equal([1L, 2L], store.Marked.Select(x => x.Sequence));
    }

    [Fact]
    public async Task MismatchedAcknowledgementLeavesMessagePending()
    {
        var message = Message(Guid.NewGuid(), 1);
        var store = new Store([message]);
        var dispatcher = new PendingSaleSyncDispatcher(store,
            new Transport(value => Ack(value) with { PayloadDigest = new string('0', 64) }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(
            Guid.NewGuid(), Guid.NewGuid(), message.DeviceId, 10, default));
        Assert.Empty(store.Marked);
    }

    [Fact]
    public async Task HttpTransportUsesVersionedScopedContract()
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var message = Message(Guid.NewGuid(), 7);
        HttpRequestMessage? captured = null;
        var client = new HttpClient(new Handler(async request =>
        {
            captured = request;
            var body = JsonSerializer.Serialize(Ack(message), JsonOptions);
            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }))
        { BaseAddress = new Uri("https://pos.test/") };

        var result = await new HttpRemoteSyncTransport(client).SendAsync(organizationId, branchId, message, default);

        Assert.Equal(message.MessageId, result.MessageId);
        Assert.Equal($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/devices/{message.DeviceId:D}/sync/messages", captured!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, captured.Method);
    }

    private static LocalOutboxMessage Message(Guid deviceId, long sequence) => new(Guid.NewGuid(), Guid.NewGuid(),
        deviceId, sequence, "sale.completed.v1", "{}", new string('A', 64), "pending", null, DateTimeOffset.UtcNow);
    private static RemoteSyncAcknowledgement Ack(LocalOutboxMessage message) => new(message.MessageId,
        message.DeviceId, message.SaleId, message.Sequence, 1, message.MessageType, "applied", "applied",
        message.PayloadDigest, DateTimeOffset.UtcNow, false);

    private sealed class Transport(Func<LocalOutboxMessage, RemoteSyncAcknowledgement> response) : IRemoteSyncTransport
    {
        public Task<RemoteSyncAcknowledgement> SendAsync(Guid organizationId, Guid branchId,
            LocalOutboxMessage message, CancellationToken cancellationToken) => Task.FromResult(response(message));
    }

    private sealed class Store(IReadOnlyList<LocalOutboxMessage> pending) : ILocalSaleStore
    {
        public List<LocalOutboxMessage> Marked { get; } = [];
        public Task<LocalSaleWriteResult> CompleteAsync(CompleteLocalSaleCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LocalOutboxMessage>> ReadPendingAsync(Guid deviceId, int limit, CancellationToken cancellationToken) => Task.FromResult(pending);
        public Task MarkResultAsync(Guid messageId, string payloadDigest, string status, string resultCode,
            DateTimeOffset acceptedAt, CancellationToken cancellationToken)
        {
            Marked.Add(pending.Single(x => x.MessageId == messageId));
            return Task.CompletedTask;
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => response(request);
    }
}
