using System.Net;
using System.Net.Http.Headers;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Infrastructure.Authentication;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class SyncCompositionTests
{
    [Fact]
    public async Task BearerHandlerObtainsTokenForEveryRequest()
    {
        AuthenticationHeaderValue? captured = null;
        var handler = new BearerTokenHandler(new Tokens())
        {
            InnerHandler = new Handler(request =>
            {
                captured = request.Headers.Authorization;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
        };

        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("https://pos.test/sync");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", captured!.Scheme);
        Assert.Equal("access-token", captured.Parameter);
    }

    [Fact]
    public async Task RunnerRetriesTransientFailuresWithBoundedExponentialBackoff()
    {
        var message = Message(); var store = new Store(message); var transport = new FlakyTransport(2,
            new HttpRequestException("Unavailable", null, HttpStatusCode.ServiceUnavailable));
        var waits = new Delay();
        var runner = new PendingSaleSyncRunner(new PendingSaleSyncDispatcher(store, transport), waits);

        Assert.Equal(1, await runner.RunAsync(Guid.NewGuid(), Guid.NewGuid(), message.DeviceId, 10, 3,
            TimeSpan.FromMilliseconds(100), default));
        Assert.Equal([TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200)], waits.Values);
        Assert.Equal(3, transport.Attempts);
        Assert.True(store.Marked);
    }

    [Fact]
    public async Task RunnerDoesNotRetryAuthenticationFailure()
    {
        var message = Message(); var store = new Store(message); var transport = new FlakyTransport(1,
            new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
        var waits = new Delay();
        var runner = new PendingSaleSyncRunner(new PendingSaleSyncDispatcher(store, transport), waits);

        await Assert.ThrowsAsync<HttpRequestException>(() => runner.RunAsync(Guid.NewGuid(), Guid.NewGuid(),
            message.DeviceId, 10, 3, TimeSpan.FromMilliseconds(100), default));
        Assert.Empty(waits.Values);
        Assert.Equal(1, transport.Attempts);
        Assert.False(store.Marked);
    }

    private static LocalOutboxMessage Message() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
        "sale.completed.v1", "{}", new string('A', 64), "pending", null, DateTimeOffset.UtcNow);

    private sealed class Tokens : IAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult("access-token");
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }

    private sealed class Delay : ISyncRetryDelay
    {
        public List<TimeSpan> Values { get; } = [];
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Values.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class FlakyTransport(int failures, HttpRequestException exception) : IRemoteSyncTransport
    {
        public int Attempts { get; private set; }
        public Task<RemoteSyncAcknowledgement> SendAsync(Guid organizationId, Guid branchId,
            LocalOutboxMessage message, CancellationToken cancellationToken)
        {
            Attempts++;
            if (Attempts <= failures) throw exception;
            return Task.FromResult(new RemoteSyncAcknowledgement(message.MessageId, message.DeviceId,
                message.SaleId, message.Sequence, 1, message.MessageType, "applied", "applied",
                message.PayloadDigest, DateTimeOffset.UtcNow, false));
        }
    }

    private sealed class Store(LocalOutboxMessage message) : ILocalSaleStore
    {
        public bool Marked { get; private set; }
        public Task<LocalSaleWriteResult> CompleteAsync(CompleteLocalSaleCommand command,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LocalOutboxMessage>> ReadPendingAsync(Guid deviceId, int limit,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalOutboxMessage>>(
                Marked ? [] : [message]);
        public Task MarkResultAsync(Guid messageId, string payloadDigest, string status, string resultCode,
            DateTimeOffset acceptedAt, CancellationToken cancellationToken)
        {
            Marked = true;
            return Task.CompletedTask;
        }
    }
}
