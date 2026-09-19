using Npgsql;
using NpgsqlTypes;
using SalekhPos.Notifications.Application.Notifications;
using SalekhPos.Notifications.Contracts.Notifications;

namespace SalekhPos.Notifications.Infrastructure.Notifications;

public sealed class PostgresNotificationDeliveryStore(NpgsqlDataSource? source) : INotificationDeliveryStore
{
    private sealed record ManualRetryRow(Guid DeliveryId, string Reason);

    public async Task<NotificationDeliveryPage> ListAsync(
        NotificationIdentity identity,
        Guid organizationId,
        int pageSize,
        Guid? after,
        string? status,
        string? channel,
        CancellationToken cancellationToken)
    {
        identity.Validate();
        status = OptionalStatus(status);
        channel = OptionalChannel(channel);
        if (organizationId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty)
        {
            throw new ArgumentException("Notification delivery query is invalid.");
        }

        var data = source ?? throw new NotificationUnavailableException();
        await using var connection = await data.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(
            connection,
            transaction,
            organizationId,
            identity,
            "notifications.manage",
            cancellationToken);

        await using var query = new NpgsqlCommand("""
            SELECT d.delivery_id,d.notification_id,d.channel,d.recipient_subject,d.status,
              d.attempt_count,d.next_attempt_at,d.last_error_code,d.created_at,d.updated_at,
              n.title,n.severity
            FROM notifications.external_deliveries d
            JOIN notifications.inbox n
              ON n.organization_id=d.organization_id AND n.notification_id=d.notification_id
            WHERE d.organization_id=$1
              AND ($2::uuid IS NULL OR EXISTS(
                SELECT 1 FROM notifications.external_deliveries cursor
                WHERE cursor.organization_id=$1 AND cursor.delivery_id=$2
                  AND (d.created_at,d.delivery_id)<(cursor.created_at,cursor.delivery_id)))
              AND ($3::text IS NULL OR d.status=$3)
              AND ($4::text IS NULL OR d.channel=$4)
            ORDER BY d.created_at DESC,d.delivery_id DESC
            LIMIT $5
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = after.HasValue ? after.Value : DBNull.Value
        });
        query.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Text,
            Value = status is null ? DBNull.Value : status
        });
        query.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Text,
            Value = channel is null ? DBNull.Value : channel
        });
        query.Parameters.AddWithValue(pageSize + 1);

        var rows = new List<NotificationDeliveryActivityResponse>(pageSize + 1);
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadActivity(reader));
            }
        }

        Guid? nextCursor = null;
        if (rows.Count > pageSize)
        {
            rows.RemoveAt(pageSize);
            nextCursor = rows[^1].Id;
        }

        await transaction.CommitAsync(cancellationToken);
        return new(rows.AsReadOnly(), nextCursor);
    }

    public async Task<NotificationDeliveryActivityResponse> RetryDeadLetterAsync(
        NotificationIdentity identity,
        Guid organizationId,
        Guid deliveryId,
        Guid operationId,
        string reason,
        CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || deliveryId == Guid.Empty || operationId == Guid.Empty)
        {
            throw new ArgumentException("Notification delivery manual retry is invalid.");
        }

        reason = RequiredReason(reason);
        var data = Data();
        await using var connection = await data.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(
            connection,
            transaction,
            organizationId,
            identity,
            "notifications.manage",
            cancellationToken);

        await using (var idempotencyLock = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended($1::text,0))",
            connection,
            transaction))
        {
            idempotencyLock.Parameters.AddWithValue(
                $"{organizationId:D}:{operationId:D}");
            await idempotencyLock.ExecuteNonQueryAsync(cancellationToken);
        }

        var replay = await ReadManualRetry(
            connection, transaction, organizationId, operationId, cancellationToken);
        if (replay is not null)
        {
            EnsureSameRetry(replay, deliveryId, reason);
            var replayed = await ReadActivityById(
                connection, transaction, organizationId, deliveryId, cancellationToken)
                ?? throw new NotificationNotFoundException();
            await transaction.CommitAsync(cancellationToken);
            return replayed;
        }

        int previousAttempts;
        await using (var state = new NpgsqlCommand("""
            SELECT attempt_count
            FROM notifications.external_deliveries
            WHERE organization_id=$1 AND delivery_id=$2 AND status='dead_lettered'
            FOR UPDATE
            """, connection, transaction))
        {
            state.Parameters.AddWithValue(organizationId);
            state.Parameters.AddWithValue(deliveryId);
            var value = await state.ExecuteScalarAsync(cancellationToken);
            if (value is null)
            {
                var existing = await ReadActivityById(
                    connection, transaction, organizationId, deliveryId, cancellationToken);
                if (existing is not null) throw new NotificationConflictException();
                throw new NotificationNotFoundException();
            }
            previousAttempts = (int)value;
        }

        await using (var update = new NpgsqlCommand("""
            UPDATE notifications.external_deliveries SET
              status='pending',
              attempt_count=0,
              next_attempt_at=NULL,
              lease_id=NULL,
              lease_expires_at=NULL,
              last_error_code=NULL,
              updated_at=statement_timestamp()
            WHERE organization_id=$1 AND delivery_id=$2 AND status='dead_lettered'
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(organizationId);
            update.Parameters.AddWithValue(deliveryId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new NotificationConflictException();
            }
        }

        await using (var audit = new NpgsqlCommand("""
            INSERT INTO notifications.delivery_manual_retries(
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

        var result = await ReadActivityById(
            connection, transaction, organizationId, deliveryId, cancellationToken)
            ?? throw new NotificationNotFoundException();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<LeasedNotificationDeliveryResponse?> LeaseNextAsync(
        NotificationIdentity identity,
        Guid organizationId,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || leaseSeconds is < 10 or > 300)
        {
            throw new ArgumentException("Notification delivery lease is invalid.");
        }

        var data = source ?? throw new NotificationUnavailableException();
        await using var connection = await data.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(
            connection,
            transaction,
            organizationId,
            identity,
            "notifications.dispatch",
            cancellationToken);

        var leaseId = Guid.NewGuid();
        await using var command = new NpgsqlCommand("""
            WITH candidate AS (
              SELECT d.delivery_id
              FROM notifications.external_deliveries d
              WHERE d.organization_id=$1
                AND (
                  (d.status IN('pending','failed')
                    AND (d.next_attempt_at IS NULL OR d.next_attempt_at<=statement_timestamp()))
                  OR (d.status='delivering' AND d.lease_expires_at<=statement_timestamp())
                )
              ORDER BY d.created_at,d.delivery_id
              FOR UPDATE SKIP LOCKED
              LIMIT 1
            )
            UPDATE notifications.external_deliveries d SET
              status='delivering',
              lease_id=$2,
              lease_expires_at=statement_timestamp()+make_interval(secs=>$3),
              updated_at=statement_timestamp()
            FROM candidate x,notifications.inbox n
            WHERE d.organization_id=$1 AND d.delivery_id=x.delivery_id
              AND n.organization_id=d.organization_id AND n.notification_id=d.notification_id
            RETURNING d.delivery_id,d.notification_id,d.channel,d.recipient_subject,d.status,
              d.attempt_count,d.next_attempt_at,d.last_error_code,d.created_at,d.updated_at,
              n.title,n.body,n.severity
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.AddWithValue(leaseId);
        command.Parameters.AddWithValue(leaseSeconds);

        LeasedNotificationDeliveryResponse? result = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                result = new(
                    ReadDelivery(reader),
                    leaseId,
                    reader.GetString(10),
                    reader.GetString(11),
                    reader.GetString(12));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<NotificationDeliveryResponse> RecordAttemptAsync(
        NotificationIdentity identity,
        Guid organizationId,
        Guid deliveryId,
        RecordNotificationDeliveryAttemptRequest request,
        CancellationToken cancellationToken)
    {
        identity.Validate();
        var now = DateTimeOffset.UtcNow;
        if (organizationId == Guid.Empty || deliveryId == Guid.Empty || request.LeaseId == Guid.Empty
            || (request.Succeeded && (request.RetryAt.HasValue || request.ErrorCode is not null))
            || (!request.Succeeded && string.IsNullOrWhiteSpace(request.ErrorCode))
            || (!request.Succeeded && request.RetryAt.HasValue && request.RetryAt <= now))
        {
            throw new ArgumentException("Notification delivery attempt is invalid.");
        }

        var errorCode = request.ErrorCode is null ? null : Required(request.ErrorCode, 100);
        var data = source ?? throw new NotificationUnavailableException();
        await using var connection = await data.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(
            connection,
            transaction,
            organizationId,
            identity,
            "notifications.dispatch",
            cancellationToken);

        await using var update = new NpgsqlCommand("""
            UPDATE notifications.external_deliveries SET
              attempt_count=attempt_count+1,
              status=CASE
                WHEN $4 THEN 'delivered'
                WHEN $5::timestamptz IS NULL OR attempt_count+1>=10 THEN 'dead_lettered'
                ELSE 'failed'
              END,
              next_attempt_at=CASE
                WHEN $4 OR $5::timestamptz IS NULL OR attempt_count+1>=10 THEN NULL
                ELSE $5
              END,
              lease_id=NULL,
              lease_expires_at=NULL,
              last_error_code=$6,
              updated_at=statement_timestamp()
            WHERE organization_id=$1 AND delivery_id=$2
              AND status='delivering' AND lease_id=$3
            RETURNING delivery_id,notification_id,channel,recipient_subject,status,attempt_count,
              next_attempt_at,last_error_code,created_at,updated_at
            """, connection, transaction);
        update.Parameters.AddWithValue(organizationId);
        update.Parameters.AddWithValue(deliveryId);
        update.Parameters.AddWithValue(request.LeaseId);
        update.Parameters.AddWithValue(request.Succeeded);
        update.Parameters.AddWithValue(request.RetryAt.HasValue ? request.RetryAt.Value : DBNull.Value);
        update.Parameters.AddWithValue(errorCode is null ? DBNull.Value : errorCode);

        NotificationDeliveryResponse result;
        await using (var reader = await update.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new NotificationConflictException();
            }
            result = ReadDelivery(reader);
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private NpgsqlDataSource Data() => source ?? throw new NotificationUnavailableException();

    private static async Task<ManualRetryRow?> ReadManualRetry(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT delivery_id,reason
            FROM notifications.delivery_manual_retries
            WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(operationId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ManualRetryRow(reader.GetGuid(0), reader.GetString(1))
            : null;
    }

    private static async Task<NotificationDeliveryActivityResponse?> ReadActivityById(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT d.delivery_id,d.notification_id,d.channel,d.recipient_subject,d.status,
              d.attempt_count,d.next_attempt_at,d.last_error_code,d.created_at,d.updated_at,
              n.title,n.severity
            FROM notifications.external_deliveries d
            JOIN notifications.inbox n
              ON n.organization_id=d.organization_id AND n.notification_id=d.notification_id
            WHERE d.organization_id=$1 AND d.delivery_id=$2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(deliveryId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadActivity(reader) : null;
    }

    private static void EnsureSameRetry(ManualRetryRow replay, Guid deliveryId, string reason)
    {
        if (replay.DeliveryId != deliveryId || !string.Equals(replay.Reason, reason, StringComparison.Ordinal))
        {
            throw new NotificationConflictException();
        }
    }

    private static string RequiredReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Notification delivery retry reason is invalid.");
        }
        value = value.Trim();
        if (value.Length > 500 || value.Any(char.IsControl))
        {
            throw new ArgumentException("Notification delivery retry reason is invalid.");
        }
        return value;
    }

    private static NotificationDeliveryActivityResponse ReadActivity(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetInt32(5),
        reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.GetFieldValue<DateTimeOffset>(8),
        reader.GetFieldValue<DateTimeOffset>(9),
        reader.GetString(10),
        reader.GetString(11));

    private static string? OptionalStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().ToLowerInvariant();
        return value is "pending" or "delivering" or "failed" or "delivered" or "dead_lettered"
            ? value
            : throw new ArgumentException("Notification delivery status is invalid.");
    }

    private static string? OptionalChannel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().ToLowerInvariant();
        return value is "email" or "push"
            ? value
            : throw new ArgumentException("Notification delivery channel is invalid.");
    }

    private static NotificationDeliveryResponse ReadDelivery(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetInt32(5),
        reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.GetFieldValue<DateTimeOffset>(8),
        reader.GetFieldValue<DateTimeOffset>(9));

    private static string Required(string value, int maximum)
    {
        value = value.Trim();
        if (value.Length is < 1 || value.Length > maximum || value.Any(char.IsControl))
        {
            throw new ArgumentException("Notification delivery error code is invalid.");
        }
        return value;
    }

    private static async Task Prepare(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        NotificationIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(
                SELECT FROM pg_class x
                JOIN pg_namespace n ON n.oid=x.relnamespace
                WHERE n.nspname='notifications' AND x.relname='external_deliveries'
                  AND x.relrowsecurity AND x.relforcerowsecurity
                  AND x.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw new NotificationUnavailableException();
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
        NotificationIdentity identity,
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
            throw new NotificationDeniedException();
        }
    }
}
