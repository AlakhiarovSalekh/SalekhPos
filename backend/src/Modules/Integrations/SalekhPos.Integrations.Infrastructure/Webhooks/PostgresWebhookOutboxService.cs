using System.Security.Cryptography;
using Npgsql;
using SalekhPos.Integrations.Application;
using SalekhPos.Integrations.Contracts;
using SalekhPos.Integrations.Domain.Webhooks;

namespace SalekhPos.Integrations.Infrastructure.Webhooks;

public sealed class PostgresWebhookOutboxService(NpgsqlDataSource? source) : IWebhookOutboxService
{
    public const int MaximumPayloadBytes = 262_144;

    public async Task<IntegrationWriteResult<WebhookDeliveryResponse>> EnqueueStoredAsync(
        IntegrationIdentity identity,
        EnqueueStoredWebhookCommand command,
        CancellationToken cancellationToken)
    {
        identity.Validate();
        Validate(command);

        var eventType = IntegrationInput.Required(command.EventType, 120, nameof(command.EventType)).ToLowerInvariant();
        var digest = Convert.ToHexString(SHA256.HashData(command.Payload.Span)).ToLowerInvariant();
        _ = new WebhookDelivery(command.OrganizationId, command.DeliveryId, command.ConnectionId, command.EventId,
            eventType, digest, WebhookDeliveryStatus.Pending, 0);
        var reference = $"pgpayload://{command.OrganizationId:D}/{command.DeliveryId:D}";

        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, command.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, command.OrganizationId, identity, "integrations.dispatch", cancellationToken);

        await using var insert = new NpgsqlCommand("""
            INSERT INTO integrations.webhook_deliveries(
              organization_id,delivery_id,operation_id,connection_id,event_id,event_type,payload_sha256,payload_reference)
            SELECT $1,$2,$3,$4,$5,$6,$7,$8
            FROM integrations.connections
            WHERE organization_id=$1 AND connection_id=$4 AND status='active'
            ON CONFLICT DO NOTHING
            RETURNING delivery_id,connection_id,event_id,event_type,payload_sha256,status,attempt_count,next_attempt_at,
              last_status_code,last_error_code,created_at,updated_at
            """, connection, transaction);
        insert.Parameters.AddWithValue(command.OrganizationId);
        insert.Parameters.AddWithValue(command.DeliveryId);
        insert.Parameters.AddWithValue(command.OperationId);
        insert.Parameters.AddWithValue(command.ConnectionId);
        insert.Parameters.AddWithValue(command.EventId);
        insert.Parameters.AddWithValue(eventType);
        insert.Parameters.AddWithValue(digest);
        insert.Parameters.AddWithValue(reference);

        WebhookDeliveryResponse? result = null;
        await using (var reader = await insert.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                result = ReadDelivery(reader);
            }
        }

        var created = result is not null;
        if (created)
        {
            await using var payload = new NpgsqlCommand("""
                INSERT INTO integrations.webhook_payloads(
                  organization_id,delivery_id,payload_sha256,payload)
                VALUES($1,$2,$3,$4)
                """, connection, transaction);
            payload.Parameters.AddWithValue(command.OrganizationId);
            payload.Parameters.AddWithValue(command.DeliveryId);
            payload.Parameters.AddWithValue(digest);
            payload.Parameters.AddWithValue(command.Payload.ToArray());
            await payload.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            result = await ReadDeliveryByOperation(
                connection, transaction, command.OrganizationId, command.OperationId, cancellationToken);
            if (result is null
                || result.ConnectionId != command.ConnectionId
                || result.EventId != command.EventId
                || result.EventType != eventType
                || result.PayloadSha256 != digest)
            {
                throw new IntegrationConflictException();
            }

            await using var replayPayload = new NpgsqlCommand("""
                SELECT payload_sha256
                FROM integrations.webhook_payloads
                WHERE organization_id=$1 AND delivery_id=$2
                """, connection, transaction);
            replayPayload.Parameters.AddWithValue(command.OrganizationId);
            replayPayload.Parameters.AddWithValue(result.Id);
            var storedDigest = await replayPayload.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(storedDigest, digest, StringComparison.Ordinal))
            {
                throw new IntegrationConflictException();
            }
        }

        var finalResult = result ?? throw new IntegrationUnavailableException();
        await transaction.CommitAsync(cancellationToken);
        return new(finalResult, created);
    }

    private NpgsqlDataSource Data() => source ?? throw new IntegrationUnavailableException();

    private static void Validate(EnqueueStoredWebhookCommand command)
    {
        if (command.OrganizationId == Guid.Empty
            || command.DeliveryId == Guid.Empty
            || command.OperationId == Guid.Empty
            || command.ConnectionId == Guid.Empty
            || command.EventId == Guid.Empty
            || command.Payload.Length is < 1 or > MaximumPayloadBytes)
        {
            throw new ArgumentException("Stored webhook command is invalid.");
        }
    }

    private static WebhookDeliveryResponse ReadDelivery(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4),
        reader.GetString(5), reader.GetInt32(6),
        reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
        reader.IsDBNull(8) ? null : reader.GetInt32(8),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.GetFieldValue<DateTimeOffset>(10), reader.GetFieldValue<DateTimeOffset>(11));

    private static async Task<WebhookDeliveryResponse?> ReadDeliveryByOperation(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT delivery_id,connection_id,event_id,event_type,payload_sha256,status,attempt_count,next_attempt_at,
              last_status_code,last_error_code,created_at,updated_at
            FROM integrations.webhook_deliveries
            WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(operationId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDelivery(reader) : null;
    }

    private static async Task Prepare(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        IntegrationIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(
                SELECT FROM pg_class x
                JOIN pg_namespace n ON n.oid=x.relnamespace
                WHERE n.nspname='integrations' AND x.relname='webhook_payloads'
                  AND x.relrowsecurity AND x.relforcerowsecurity
                  AND x.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw new IntegrationUnavailableException();
        }

        await using var context = new NpgsqlCommand("""
            SELECT set_config('app.organization_id',$1,true),
              set_config('app.issuer',$2,true),
              set_config('app.subject',$3,true)
            """, connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString());
        context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject);
        await context.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Demand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        IntegrationIdentity identity,
        string permission,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS(
              SELECT FROM access.memberships m
              JOIN access.permission_grants g
                ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3
                AND m.is_active AND m.valid_from<=statement_timestamp()
                AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission=$4 AND g.scope_kind='organization')
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(identity.Issuer);
        query.Parameters.AddWithValue(identity.Subject);
        query.Parameters.AddWithValue(permission);
        if (await query.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw new IntegrationDeniedException();
        }
    }
}
