using Npgsql;
using NpgsqlTypes;
using SalekhPos.Integrations.Application;
using SalekhPos.Integrations.Contracts;

namespace SalekhPos.Integrations.Infrastructure.Integrations;

public sealed class PostgresIntegrationService(NpgsqlDataSource? source) : IIntegrationService
{
    public async Task<IntegrationWriteResult<IntegrationConnectionResponse>> CreateConnectionAsync(
        IntegrationIdentity identity, CreateIntegrationConnectionCommand command, CancellationToken cancellationToken)
    {
        identity.Validate();
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
        var model = command.ToModel();
        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, model.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, model.OrganizationId, identity, "integrations.manage", cancellationToken);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO integrations.connections(organization_id,connection_id,operation_id,provider,display_name,endpoint,secret_reference)
            VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING connection_id,provider,display_name,endpoint,status,created_at,updated_at
            """, connection, transaction);
        insert.Parameters.AddWithValue(model.OrganizationId); insert.Parameters.AddWithValue(model.Id);
        insert.Parameters.AddWithValue(command.OperationId); insert.Parameters.AddWithValue(model.Provider);
        insert.Parameters.AddWithValue(model.DisplayName); insert.Parameters.AddWithValue(model.Endpoint.AbsoluteUri);
        insert.Parameters.AddWithValue(model.SecretReference);
        IntegrationConnectionResponse? result = null;
        await using (var reader = await insert.ExecuteReaderAsync(cancellationToken))
            if (await reader.ReadAsync(cancellationToken)) result = ReadConnection(reader);
        var created = result is not null;
        result ??= await ReadConnectionByOperation(connection, transaction, model.OrganizationId, command.OperationId, cancellationToken)
            ?? throw new IntegrationUnavailableException();
        if (!created && (result.Id != model.Id || result.Provider != model.Provider || result.DisplayName != model.DisplayName
            || result.Endpoint != model.Endpoint.AbsoluteUri)) throw new IntegrationConflictException();
        await transaction.CommitAsync(cancellationToken);
        return new(result, created);
    }

    public async Task<IntegrationConnectionPage> ListConnectionsAsync(IntegrationIdentity identity, Guid organizationId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        identity.Validate(); ValidatePage(organizationId, pageSize, after);
        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "integrations.view", cancellationToken);
        await using var query = new NpgsqlCommand("""
            SELECT connection_id,provider,display_name,endpoint,status,created_at,updated_at FROM integrations.connections
            WHERE organization_id=$1 AND ($2::uuid IS NULL OR connection_id>$2) ORDER BY connection_id LIMIT $3
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.Add(NullableUuid(after)); query.Parameters.AddWithValue(pageSize + 1);
        var rows = new List<IntegrationConnectionResponse>();
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) rows.Add(ReadConnection(reader));
        Guid? next = null; if (rows.Count > pageSize) { rows.RemoveAt(pageSize); next = rows[^1].Id; }
        await transaction.CommitAsync(cancellationToken); return new(rows, next);
    }

    public async Task<IntegrationConnectionResponse> DisableConnectionAsync(IntegrationIdentity identity, Guid organizationId,
        Guid connectionId, Guid operationId, string reason, CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || connectionId == Guid.Empty || operationId == Guid.Empty) throw new ArgumentException("Connection mutation is invalid.");
        reason = IntegrationInput.Required(reason, 500, nameof(reason));
        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "integrations.manage", cancellationToken);
        await using (var replay = new NpgsqlCommand("SELECT connection_id,reason FROM integrations.connection_state_changes WHERE organization_id=$1 AND operation_id=$2", connection, transaction))
        {
            replay.Parameters.AddWithValue(organizationId); replay.Parameters.AddWithValue(operationId);
            await using var reader = await replay.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetGuid(0) != connectionId || reader.GetString(1) != reason) throw new IntegrationConflictException();
                await reader.DisposeAsync();
                var existing = await ReadConnection(connection, transaction, organizationId, connectionId, cancellationToken) ?? throw new IntegrationNotFoundException();
                await transaction.CommitAsync(cancellationToken); return existing;
            }
        }
        await using (var update = new NpgsqlCommand("""
            UPDATE integrations.connections SET status='disabled',disabled_reason=$3,updated_at=statement_timestamp()
            WHERE organization_id=$1 AND connection_id=$2 AND status='active'
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(organizationId); update.Parameters.AddWithValue(connectionId); update.Parameters.AddWithValue(reason);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) throw new IntegrationConflictException();
        }
        await using (var audit = new NpgsqlCommand("""
            INSERT INTO integrations.connection_state_changes(organization_id,change_id,connection_id,operation_id,from_status,to_status,reason,changed_by_issuer,changed_by_subject)
            VALUES($1,$2,$3,$4,'active','disabled',$5,$6,$7)
            """, connection, transaction))
        {
            audit.Parameters.AddWithValue(organizationId); audit.Parameters.AddWithValue(Guid.NewGuid()); audit.Parameters.AddWithValue(connectionId);
            audit.Parameters.AddWithValue(operationId); audit.Parameters.AddWithValue(reason); audit.Parameters.AddWithValue(identity.Issuer); audit.Parameters.AddWithValue(identity.Subject);
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }
        var result = await ReadConnection(connection, transaction, organizationId, connectionId, cancellationToken) ?? throw new IntegrationNotFoundException();
        await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<IntegrationWriteResult<WebhookDeliveryResponse>> EnqueueWebhookAsync(IntegrationIdentity identity,
        EnqueueWebhookCommand command, CancellationToken cancellationToken)
    {
        identity.Validate(); if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
        var model = command.ToModel();
        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, model.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, model.OrganizationId, identity, "integrations.dispatch", cancellationToken);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO integrations.webhook_deliveries(organization_id,delivery_id,operation_id,connection_id,event_id,event_type,payload_sha256,payload_reference)
            SELECT $1,$2,$3,$4,$5,$6,$7,$8 FROM integrations.connections
            WHERE organization_id=$1 AND connection_id=$4 AND status='active'
            ON CONFLICT DO NOTHING
            RETURNING delivery_id,connection_id,event_id,event_type,payload_sha256,status,attempt_count,next_attempt_at,last_status_code,last_error_code,created_at,updated_at
            """, connection, transaction);
        insert.Parameters.AddWithValue(model.OrganizationId); insert.Parameters.AddWithValue(model.Id); insert.Parameters.AddWithValue(command.OperationId);
        insert.Parameters.AddWithValue(model.ConnectionId); insert.Parameters.AddWithValue(model.EventId); insert.Parameters.AddWithValue(model.EventType);
        insert.Parameters.AddWithValue(model.PayloadSha256); insert.Parameters.AddWithValue(command.PayloadReference);
        WebhookDeliveryResponse? result = null;
        await using (var reader = await insert.ExecuteReaderAsync(cancellationToken)) if (await reader.ReadAsync(cancellationToken)) result = ReadDelivery(reader);
        var created = result is not null;
        result ??= await ReadDeliveryByOperation(connection, transaction, model.OrganizationId, command.OperationId, cancellationToken);
        if (result is null) throw new IntegrationConflictException();
        if (!created && (result.Id != model.Id || result.ConnectionId != model.ConnectionId || result.EventId != model.EventId
            || result.EventType != model.EventType || result.PayloadSha256 != model.PayloadSha256)) throw new IntegrationConflictException();
        await transaction.CommitAsync(cancellationToken); return new(result, created);
    }

    public async Task<WebhookDeliveryPage> ListDeliveriesAsync(IntegrationIdentity identity, Guid organizationId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        identity.Validate(); ValidatePage(organizationId, pageSize, after);
        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "integrations.view", cancellationToken);
        await using var query = new NpgsqlCommand("""
            SELECT delivery_id,connection_id,event_id,event_type,payload_sha256,status,attempt_count,next_attempt_at,last_status_code,last_error_code,created_at,updated_at
            FROM integrations.webhook_deliveries WHERE organization_id=$1 AND ($2::uuid IS NULL OR delivery_id>$2)
            ORDER BY delivery_id LIMIT $3
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.Add(NullableUuid(after)); query.Parameters.AddWithValue(pageSize + 1);
        var rows = new List<WebhookDeliveryResponse>();
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken)) while (await reader.ReadAsync(cancellationToken)) rows.Add(ReadDelivery(reader));
        Guid? next = null; if (rows.Count > pageSize) { rows.RemoveAt(pageSize); next = rows[^1].Id; }
        await transaction.CommitAsync(cancellationToken); return new(rows, next);
    }

    public async Task<LeasedWebhookResponse?> LeaseNextWebhookAsync(IntegrationIdentity identity, Guid organizationId,
        LeaseWebhookRequest request, CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || request.LeaseSeconds is < 10 or > 300)
            throw new ArgumentException("Webhook lease is invalid.");
        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "integrations.dispatch", cancellationToken);
        var leaseId = Guid.NewGuid();
        await using var command = new NpgsqlCommand("""
            WITH candidate AS (
              SELECT d.delivery_id FROM integrations.webhook_deliveries d
              JOIN integrations.connections c ON c.organization_id=d.organization_id AND c.connection_id=d.connection_id
              WHERE d.organization_id=$1 AND c.status='active'
                AND ((d.status IN('pending','failed') AND (d.next_attempt_at IS NULL OR d.next_attempt_at<=statement_timestamp()))
                  OR (d.status='delivering' AND d.lease_expires_at<=statement_timestamp()))
              ORDER BY d.created_at,d.delivery_id FOR UPDATE OF d SKIP LOCKED LIMIT 1
            )
            UPDATE integrations.webhook_deliveries d SET status='delivering',lease_id=$2,
              lease_expires_at=statement_timestamp()+make_interval(secs=>$3),updated_at=statement_timestamp()
            FROM candidate x,integrations.connections c
            WHERE d.organization_id=$1 AND d.delivery_id=x.delivery_id
              AND c.organization_id=d.organization_id AND c.connection_id=d.connection_id
            RETURNING d.delivery_id,d.connection_id,d.event_id,d.event_type,d.payload_sha256,d.status,d.attempt_count,
              d.next_attempt_at,d.last_status_code,d.last_error_code,d.created_at,d.updated_at,
              d.payload_reference,c.endpoint,c.secret_reference
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(leaseId); command.Parameters.AddWithValue(request.LeaseSeconds);
        LeasedWebhookResponse? result = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            if (await reader.ReadAsync(cancellationToken)) result = new(ReadDelivery(reader), leaseId, reader.GetString(12), reader.GetString(13), reader.GetString(14));
        await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<WebhookDeliveryResponse> RetryDeadLetterAsync(IntegrationIdentity identity,
        Guid organizationId, Guid deliveryId, Guid operationId, string reason,
        CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || deliveryId == Guid.Empty || operationId == Guid.Empty)
        {
            throw new ArgumentException("Webhook manual retry is invalid.");
        }

        reason = IntegrationInput.Required(reason, 500, nameof(reason));
        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "integrations.manage", cancellationToken);

        await using (var replay = new NpgsqlCommand("""
            SELECT delivery_id,reason
            FROM integrations.webhook_manual_retries
            WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction))
        {
            replay.Parameters.AddWithValue(organizationId);
            replay.Parameters.AddWithValue(operationId);
            await using var reader = await replay.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetGuid(0) != deliveryId || reader.GetString(1) != reason)
                {
                    throw new IntegrationConflictException();
                }

                await reader.DisposeAsync();
                var replayed = await ReadDeliveryById(connection, transaction, organizationId, deliveryId,
                    cancellationToken) ?? throw new IntegrationNotFoundException();
                await transaction.CommitAsync(cancellationToken);
                return replayed;
            }
        }

        int previousAttempts;
        await using (var state = new NpgsqlCommand("""
            SELECT d.attempt_count
            FROM integrations.webhook_deliveries d
            JOIN integrations.connections c
              ON c.organization_id=d.organization_id AND c.connection_id=d.connection_id
            WHERE d.organization_id=$1 AND d.delivery_id=$2
              AND d.status='dead_lettered' AND c.status='active'
            FOR UPDATE OF d
            """, connection, transaction))
        {
            state.Parameters.AddWithValue(organizationId);
            state.Parameters.AddWithValue(deliveryId);
            var value = await state.ExecuteScalarAsync(cancellationToken);
            if (value is null)
            {
                var existing = await ReadDeliveryById(connection, transaction, organizationId, deliveryId,
                    cancellationToken);
                if (existing is null) throw new IntegrationNotFoundException();
                throw new IntegrationConflictException();
            }
            previousAttempts = (int)value;
        }

        WebhookDeliveryResponse result;
        await using (var update = new NpgsqlCommand("""
            UPDATE integrations.webhook_deliveries SET
              status='pending',
              attempt_count=0,
              lease_id=NULL,
              lease_expires_at=NULL,
              next_attempt_at=NULL,
              last_status_code=NULL,
              last_error_code=NULL,
              updated_at=statement_timestamp()
            WHERE organization_id=$1 AND delivery_id=$2 AND status='dead_lettered'
            RETURNING delivery_id,connection_id,event_id,event_type,payload_sha256,status,attempt_count,next_attempt_at,
              last_status_code,last_error_code,created_at,updated_at
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(organizationId);
            update.Parameters.AddWithValue(deliveryId);
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new IntegrationConflictException();
            }
            result = ReadDelivery(reader);
        }

        await using (var audit = new NpgsqlCommand("""
            INSERT INTO integrations.webhook_manual_retries(
              organization_id,retry_id,delivery_id,operation_id,reason,previous_attempt_count,
              changed_by_issuer,changed_by_subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8)
            """, connection, transaction))
        {
            audit.Parameters.AddWithValue(organizationId);
            audit.Parameters.AddWithValue(Guid.NewGuid());
            audit.Parameters.AddWithValue(deliveryId);
            audit.Parameters.AddWithValue(operationId);
            audit.Parameters.AddWithValue(reason);
            audit.Parameters.AddWithValue(previousAttempts);
            audit.Parameters.AddWithValue(identity.Issuer);
            audit.Parameters.AddWithValue(identity.Subject);
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<WebhookDeliveryResponse> DeferLeaseAsync(IntegrationIdentity identity, Guid organizationId,
        Guid deliveryId, DeferWebhookLeaseCommand command, CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || deliveryId == Guid.Empty || command.LeaseId == Guid.Empty
            || command.RetryAt <= DateTimeOffset.UtcNow || command.RetryAt > DateTimeOffset.UtcNow.AddHours(24))
        {
            throw new ArgumentException("Webhook lease deferral is invalid.");
        }

        var error = IntegrationInput.Required(command.ErrorCode, 100, nameof(command.ErrorCode));
        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "integrations.dispatch", cancellationToken);

        await using var update = new NpgsqlCommand("""
            UPDATE integrations.webhook_deliveries SET
              status='failed',
              lease_id=NULL,
              lease_expires_at=NULL,
              next_attempt_at=$5,
              last_status_code=NULL,
              last_error_code=$4,
              updated_at=statement_timestamp()
            WHERE organization_id=$1 AND delivery_id=$2 AND status='delivering' AND lease_id=$3
            RETURNING delivery_id,connection_id,event_id,event_type,payload_sha256,status,attempt_count,next_attempt_at,
              last_status_code,last_error_code,created_at,updated_at
            """, connection, transaction);
        update.Parameters.AddWithValue(organizationId);
        update.Parameters.AddWithValue(deliveryId);
        update.Parameters.AddWithValue(command.LeaseId);
        update.Parameters.AddWithValue(error);
        update.Parameters.AddWithValue(command.RetryAt);

        WebhookDeliveryResponse result;
        await using (var reader = await update.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new IntegrationConflictException();
            }
            result = ReadDelivery(reader);
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<WebhookDeliveryResponse> RecordAttemptAsync(IntegrationIdentity identity, Guid organizationId,
        Guid deliveryId, RecordWebhookAttemptRequest request, CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || deliveryId == Guid.Empty || request.LeaseId == Guid.Empty
            || request.StatusCode is < 100 or > 599 || (!request.Succeeded && request.RetryAt <= DateTimeOffset.UtcNow))
            throw new ArgumentException("Webhook attempt is invalid.");
        var error = request.ErrorCode is null ? null : IntegrationInput.Required(request.ErrorCode, 100, nameof(request.ErrorCode));
        await using var connection = await Data().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "integrations.dispatch", cancellationToken);
        await using var update = new NpgsqlCommand("""
            UPDATE integrations.webhook_deliveries SET
              attempt_count=attempt_count+1,status=CASE WHEN $4 THEN 'delivered' WHEN $7 IS NULL OR attempt_count+1>=10 THEN 'dead_lettered' ELSE 'failed' END,
              lease_id=NULL,lease_expires_at=NULL,next_attempt_at=CASE WHEN $4 OR $7 IS NULL OR attempt_count+1>=10 THEN NULL ELSE $7 END,
              last_status_code=$5,last_error_code=$6,updated_at=statement_timestamp()
            WHERE organization_id=$1 AND delivery_id=$2 AND status='delivering' AND lease_id=$3
            RETURNING delivery_id,connection_id,event_id,event_type,payload_sha256,status,attempt_count,next_attempt_at,last_status_code,last_error_code,created_at,updated_at
            """, connection, transaction);
        update.Parameters.AddWithValue(organizationId); update.Parameters.AddWithValue(deliveryId); update.Parameters.AddWithValue(request.LeaseId);
        update.Parameters.AddWithValue(request.Succeeded); update.Parameters.Add(NullableInt(request.StatusCode)); update.Parameters.Add(NullableText(error));
        update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = request.RetryAt.HasValue ? request.RetryAt.Value : DBNull.Value });
        WebhookDeliveryResponse result;
        await using (var reader = await update.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) throw new IntegrationConflictException(); result = ReadDelivery(reader);
        }
        await using var attempt = new NpgsqlCommand("""
            INSERT INTO integrations.webhook_attempts(organization_id,attempt_id,delivery_id,attempt_number,lease_id,succeeded,status_code,error_code,attempted_by_issuer,attempted_by_subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
            """, connection, transaction);
        attempt.Parameters.AddWithValue(organizationId); attempt.Parameters.AddWithValue(Guid.NewGuid()); attempt.Parameters.AddWithValue(deliveryId);
        attempt.Parameters.AddWithValue(result.AttemptCount); attempt.Parameters.AddWithValue(request.LeaseId); attempt.Parameters.AddWithValue(request.Succeeded);
        attempt.Parameters.Add(NullableInt(request.StatusCode)); attempt.Parameters.Add(NullableText(error)); attempt.Parameters.AddWithValue(identity.Issuer); attempt.Parameters.AddWithValue(identity.Subject);
        await attempt.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken); return result;
    }

    private NpgsqlDataSource Data() => source ?? throw new IntegrationUnavailableException();
    private static void ValidatePage(Guid organizationId, int pageSize, Guid? after)
    { if (organizationId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty) throw new ArgumentException("Integration query is invalid."); }
    private static NpgsqlParameter NullableUuid(Guid? value) => new() { NpgsqlDbType = NpgsqlDbType.Uuid, Value = value.HasValue ? value.Value : DBNull.Value };
    private static NpgsqlParameter NullableInt(int? value) => new() { NpgsqlDbType = NpgsqlDbType.Integer, Value = value.HasValue ? value.Value : DBNull.Value };
    private static NpgsqlParameter NullableText(string? value) => new() { NpgsqlDbType = NpgsqlDbType.Text, Value = value is null ? DBNull.Value : value };
    private static IntegrationConnectionResponse ReadConnection(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5), r.GetFieldValue<DateTimeOffset>(6));
    private static WebhookDeliveryResponse ReadDelivery(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt32(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), r.IsDBNull(8) ? null : r.GetInt32(8), r.IsDBNull(9) ? null : r.GetString(9), r.GetFieldValue<DateTimeOffset>(10), r.GetFieldValue<DateTimeOffset>(11));
    private static async Task<IntegrationConnectionResponse?> ReadConnectionByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid o, Guid op, CancellationToken ct)
    { await using var q = new NpgsqlCommand("SELECT connection_id,provider,display_name,endpoint,status,created_at,updated_at FROM integrations.connections WHERE organization_id=$1 AND operation_id=$2", c, t); q.Parameters.AddWithValue(o); q.Parameters.AddWithValue(op); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadConnection(r) : null; }
    private static async Task<IntegrationConnectionResponse?> ReadConnection(NpgsqlConnection c, NpgsqlTransaction t, Guid o, Guid id, CancellationToken ct)
    { await using var q = new NpgsqlCommand("SELECT connection_id,provider,display_name,endpoint,status,created_at,updated_at FROM integrations.connections WHERE organization_id=$1 AND connection_id=$2", c, t); q.Parameters.AddWithValue(o); q.Parameters.AddWithValue(id); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadConnection(r) : null; }
    private static async Task<WebhookDeliveryResponse?> ReadDeliveryById(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId, Guid deliveryId,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT delivery_id,connection_id,event_id,event_type,payload_sha256,status,attempt_count,next_attempt_at,
              last_status_code,last_error_code,created_at,updated_at
            FROM integrations.webhook_deliveries
            WHERE organization_id=$1 AND delivery_id=$2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(deliveryId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDelivery(reader) : null;
    }

    private static async Task<WebhookDeliveryResponse?> ReadDeliveryByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid o, Guid op, CancellationToken ct)
    { await using var q = new NpgsqlCommand("SELECT delivery_id,connection_id,event_id,event_type,payload_sha256,status,attempt_count,next_attempt_at,last_status_code,last_error_code,created_at,updated_at FROM integrations.webhook_deliveries WHERE organization_id=$1 AND operation_id=$2", c, t); q.Parameters.AddWithValue(o); q.Parameters.AddWithValue(op); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadDelivery(r) : null; }
    private static async Task Prepare(NpgsqlConnection c, NpgsqlTransaction t, Guid o, IntegrationIdentity i, CancellationToken ct)
    { await using var safety = new NpgsqlCommand("SELECT current_user='salekhpos_runtime' AND EXISTS(SELECT FROM pg_class x JOIN pg_namespace n ON n.oid=x.relnamespace WHERE n.nspname='integrations' AND x.relname='connections' AND x.relrowsecurity AND x.relforcerowsecurity AND x.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))", c, t); if (await safety.ExecuteScalarAsync(ct) is not true) throw new IntegrationUnavailableException(); await using var context = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t); context.Parameters.AddWithValue(o.ToString()); context.Parameters.AddWithValue(i.Issuer); context.Parameters.AddWithValue(i.Subject); await context.ExecuteNonQueryAsync(ct); }
    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, Guid o, IntegrationIdentity i, string permission, CancellationToken ct)
    { await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$4 AND g.scope_kind='organization')", c, t); q.Parameters.AddWithValue(o); q.Parameters.AddWithValue(i.Issuer); q.Parameters.AddWithValue(i.Subject); q.Parameters.AddWithValue(permission); if (await q.ExecuteScalarAsync(ct) is not true) throw new IntegrationDeniedException(); }
}
