using System.Net;
using System.Net.Sockets;
using SalekhPos.Integrations.Domain.Security;

namespace SalekhPos.Integrations.Infrastructure.Webhooks;

public sealed class SafeWebhookHttpSender : IWebhookHttpSender, IDisposable
{
    private readonly HttpClient client;

    public SafeWebhookHttpSender()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            MaxResponseHeadersLength = 32,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = ConnectPublicAsync,
        };
        client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestUri is null) throw new ArgumentException("Webhook request URI is required.", nameof(request));
        _ = WebhookEndpointPolicy.ValidateUri(request.RequestUri.AbsoluteUri);
        return client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    public void Dispose() => client.Dispose();

    private static async ValueTask<Stream> ConnectPublicAsync(SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        WebhookEndpointPolicy.ValidateResolvedAddresses(addresses);

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception error) when (error is SocketException or IOException)
            {
                lastError = error;
                socket.Dispose();
            }
        }

        throw new HttpRequestException("Webhook endpoint could not be connected safely.", lastError);
    }
}
