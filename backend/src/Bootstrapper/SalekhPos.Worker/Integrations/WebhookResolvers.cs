using System.Security.Cryptography;
using SalekhPos.Integrations.Infrastructure.Webhooks;

namespace SalekhPos.Worker.Integrations;

public interface IWebhookPayloadSource
{
    bool Supports(Uri reference);

    ValueTask<ReadOnlyMemory<byte>> ResolveAsync(Uri reference, CancellationToken cancellationToken);
}

public interface IWebhookSecretSource
{
    bool Supports(Uri reference);

    ValueTask<byte[]> ResolveAsync(Uri reference, CancellationToken cancellationToken);
}

public sealed class CompositeWebhookPayloadResolver(IEnumerable<IWebhookPayloadSource> sources) : IWebhookPayloadResolver
{
    private readonly IReadOnlyList<IWebhookPayloadSource> registered = sources.ToArray();

    public bool IsReady => registered.Count > 0;

    public ValueTask<ReadOnlyMemory<byte>> ResolveAsync(string reference, CancellationToken cancellationToken)
    {
        var uri = Parse(reference);
        var source = registered.SingleOrDefault(candidate => candidate.Supports(uri))
            ?? throw new WebhookResolverUnavailableException("No webhook payload source supports the configured reference.");
        return source.ResolveAsync(uri, cancellationToken);
    }

    private static Uri Parse(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 512
            || !Uri.TryCreate(reference, UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Webhook payload reference is invalid.", nameof(reference));
        }

        return uri;
    }
}

public sealed class CompositeWebhookSecretResolver(IEnumerable<IWebhookSecretSource> sources) : IWebhookSecretResolver
{
    private readonly IReadOnlyList<IWebhookSecretSource> registered = sources.ToArray();

    public bool IsReady => registered.Count > 0;

    public async ValueTask<byte[]> ResolveAsync(string reference, CancellationToken cancellationToken)
    {
        var uri = Parse(reference);
        var source = registered.SingleOrDefault(candidate => candidate.Supports(uri))
            ?? throw new WebhookResolverUnavailableException("No webhook secret source supports the configured reference.");
        var secret = await source.ResolveAsync(uri, cancellationToken);
        if (secret.Length is < 32 or > 4096)
        {
            CryptographicOperations.ZeroMemory(secret);
            throw new WebhookResolverUnavailableException("Resolved webhook secret has an invalid length.");
        }

        return secret;
    }

    private static Uri Parse(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 512
            || !Uri.TryCreate(reference, UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Webhook secret reference is invalid.", nameof(reference));
        }

        return uri;
    }
}

public sealed class EnvironmentWebhookSecretSource : IWebhookSecretSource
{
    public bool Supports(Uri reference) =>
        string.Equals(reference.Scheme, "env", StringComparison.OrdinalIgnoreCase);

    public ValueTask<byte[]> ResolveAsync(Uri reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var original = reference.OriginalString;
        var prefix = "env://";
        var name = original.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? original[prefix.Length..]
            : string.Empty;
        if (!Supports(reference)
            || name.Length is < 1 or > 128
            || name.Contains('/')
            || name.Contains('?')
            || name.Contains('#')
            || name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))
        {
            throw new ArgumentException("Environment secret reference is invalid.", nameof(reference));
        }

        var value = Environment.GetEnvironmentVariable(name)
            ?? throw new WebhookResolverUnavailableException("The referenced environment secret is unavailable.");
        if (value.Length is < 32 or > 4096 || value.Any(char.IsControl))
        {
            throw new WebhookResolverUnavailableException("The referenced environment secret is invalid.");
        }

        return ValueTask.FromResult(System.Text.Encoding.UTF8.GetBytes(value));
    }
}
