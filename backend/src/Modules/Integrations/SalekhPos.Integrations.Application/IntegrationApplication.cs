using SalekhPos.Integrations.Contracts;
using SalekhPos.Integrations.Domain.IntegrationConnections;
using SalekhPos.Integrations.Domain.Webhooks;

namespace SalekhPos.Integrations.Application;

public sealed record IntegrationIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        IntegrationInput.Required(Issuer, 2048, nameof(Issuer));
        IntegrationInput.Required(Subject, 256, nameof(Subject));
    }
}

public static class IntegrationInput
{
    public static string Required(string? value, int maximum, string name)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length is < 1 || value.Length > maximum || value.Any(char.IsControl))
            throw new ArgumentException("Integration input is invalid.", name);
        return value;
    }
}

public sealed record CreateIntegrationConnectionCommand(Guid OrganizationId, Guid ConnectionId, Guid OperationId,
    string Provider, string DisplayName, string Endpoint, string SecretReference)
{
    public IntegrationConnection ToModel() => new(OrganizationId, ConnectionId, Provider, DisplayName, Endpoint,
        SecretReference, IntegrationConnectionStatus.Active);
}

public sealed record EnqueueWebhookCommand(Guid OrganizationId, Guid DeliveryId, Guid OperationId,
    Guid ConnectionId, Guid EventId, string EventType, string PayloadSha256, string PayloadReference)
{
    public WebhookDelivery ToModel()
    {
        IntegrationInput.Required(PayloadReference, 512, nameof(PayloadReference));
        return new(OrganizationId, DeliveryId, ConnectionId, EventId, EventType, PayloadSha256,
            WebhookDeliveryStatus.Pending, 0);
    }
}

public sealed record IntegrationWriteResult<T>(T Value, bool Created);

public interface IIntegrationService
{
    Task<IntegrationWriteResult<IntegrationConnectionResponse>> CreateConnectionAsync(IntegrationIdentity identity, CreateIntegrationConnectionCommand command, CancellationToken cancellationToken);
    Task<IntegrationConnectionPage> ListConnectionsAsync(IntegrationIdentity identity, Guid organizationId, int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<IntegrationConnectionResponse> DisableConnectionAsync(IntegrationIdentity identity, Guid organizationId, Guid connectionId, Guid operationId, string reason, CancellationToken cancellationToken);
    Task<IntegrationWriteResult<WebhookDeliveryResponse>> EnqueueWebhookAsync(IntegrationIdentity identity, EnqueueWebhookCommand command, CancellationToken cancellationToken);
    Task<WebhookDeliveryPage> ListDeliveriesAsync(IntegrationIdentity identity, Guid organizationId, int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<LeasedWebhookResponse?> LeaseNextWebhookAsync(IntegrationIdentity identity, Guid organizationId, LeaseWebhookRequest request, CancellationToken cancellationToken);
    Task<WebhookDeliveryResponse> RecordAttemptAsync(IntegrationIdentity identity, Guid organizationId, Guid deliveryId, RecordWebhookAttemptRequest request, CancellationToken cancellationToken);
}

public sealed class IntegrationDeniedException : Exception;
public sealed class IntegrationConflictException : Exception;
public sealed class IntegrationNotFoundException : Exception;
public sealed class IntegrationUnavailableException : Exception;
