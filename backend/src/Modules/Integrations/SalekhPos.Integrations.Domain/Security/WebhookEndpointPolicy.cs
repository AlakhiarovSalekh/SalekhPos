using System.Net;

namespace SalekhPos.Integrations.Domain.Security;

public static class WebhookEndpointPolicy
{
    public static Uri ValidateUri(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > 2048
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.IsLoopback
            || uri.HostNameType is UriHostNameType.Unknown or UriHostNameType.Basic)
        {
            throw new ArgumentException("Webhook endpoint must be a public HTTPS URI.", nameof(endpoint));
        }

        return uri;
    }

    public static void ValidateResolvedAddresses(IEnumerable<IPAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var resolved = addresses.Distinct().ToArray();
        if (resolved.Length == 0 || resolved.Any(IsUnsafe))
        {
            throw new InvalidOperationException("Webhook endpoint resolved to a non-public network address.");
        }
    }

    public static bool IsUnsafe(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.None)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.IPv6None)
            || address.Equals(IPAddress.IPv6Loopback)
            || address.IsIPv6Multicast
            || address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return IsUnsafe(address.MapToIPv4());
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var a = bytes[0];
            var b = bytes[1];
            return a == 0
                || a == 10
                || a == 127
                || (a == 100 && b is >= 64 and <= 127)
                || (a == 169 && b == 254)
                || (a == 172 && b is >= 16 and <= 31)
                || (a == 192 && b == 168)
                || (a == 198 && b is 18 or 19)
                || a >= 224;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xfe) == 0xfc;
        }

        return true;
    }
}
