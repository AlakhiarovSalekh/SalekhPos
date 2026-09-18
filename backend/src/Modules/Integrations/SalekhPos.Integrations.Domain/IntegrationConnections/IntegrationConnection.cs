namespace SalekhPos.Integrations.Domain.IntegrationConnections;

public enum IntegrationConnectionStatus { Active, Disabled }

public sealed record IntegrationConnection
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public string Provider { get; }
    public string DisplayName { get; }
    public Uri Endpoint { get; }
    public string SecretReference { get; }
    public IntegrationConnectionStatus Status { get; }

    public IntegrationConnection(Guid organizationId, Guid id, string provider, string displayName,
        string endpoint, string secretReference, IntegrationConnectionStatus status)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty) throw new ArgumentException("Connection identity is invalid.");
        Provider = IntegrationText.Required(provider, 64, nameof(provider), allowSpaces: false).ToLowerInvariant();
        DisplayName = IntegrationText.Required(displayName, 160, nameof(displayName));
        SecretReference = IntegrationText.Required(secretReference, 512, nameof(secretReference), allowSpaces: false);
        if (!Uri.TryCreate(SecretReference, UriKind.Absolute, out var secretUri)
            || secretUri.Scheme is not ("vault" or "aws-sm" or "azure-kv" or "gcp-sm" or "env")
            || !string.IsNullOrEmpty(secretUri.UserInfo) || !string.IsNullOrEmpty(secretUri.Query)
            || !string.IsNullOrEmpty(secretUri.Fragment))
            throw new ArgumentException("Secret reference must use an approved external secret-store scheme.", nameof(secretReference));
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.IsLoopback || uri.HostNameType is UriHostNameType.Unknown or UriHostNameType.Basic)
            throw new ArgumentException("Connection endpoint must be a public HTTPS URI without user information.", nameof(endpoint));
        OrganizationId = organizationId; Id = id; Endpoint = uri; Status = status;
    }
}

internal static class IntegrationText
{
    internal static string Required(string? value, int maximum, string name, bool allowSpaces = true)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length is < 1 || value.Length > maximum || value.Any(char.IsControl)
            || (!allowSpaces && value.Any(char.IsWhiteSpace))) throw new ArgumentException("Integration text is invalid.", name);
        return value;
    }
}
