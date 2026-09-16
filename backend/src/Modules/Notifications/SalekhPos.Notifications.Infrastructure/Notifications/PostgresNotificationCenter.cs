using Npgsql;
using NpgsqlTypes;
using SalekhPos.Notifications.Application.Notifications;
using SalekhPos.Notifications.Contracts.Notifications;
using SalekhPos.Notifications.Domain.Notifications;

namespace SalekhPos.Notifications.Infrastructure.Notifications;

public sealed class PostgresNotificationCenter(NpgsqlDataSource? source) : INotificationCenter
{
    private sealed record Row(Guid Id, Guid? BranchId, string Recipient, string Title, string Body,
        string Severity, bool IsRead, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

    public async Task<NotificationWriteResult> CreateAsync(NotificationIdentity identity,
        CreateNotificationCommand command, CancellationToken ct)
    {
        identity.Validate();
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
        var model = command.ToNotification();
        var data = source ?? throw new NotificationUnavailableException();
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, model.OrganizationId, identity, ct);
        await Demand(connection, transaction, model.OrganizationId, model.BranchId, identity, "notifications.manage", ct);
        if (model.BranchId.HasValue) await EnsureBranch(connection, transaction, model.OrganizationId, model.BranchId.Value, ct);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO notifications.inbox(organization_id,notification_id,operation_id,branch_id,
              recipient_subject,title,body,severity)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING notification_id,branch_id,recipient_subject,title,body,severity,is_read,created_at,read_at
            """, connection, transaction);
        insert.Parameters.AddWithValue(model.OrganizationId); insert.Parameters.AddWithValue(model.Id);
        insert.Parameters.AddWithValue(command.OperationId);
        insert.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = model.BranchId.HasValue ? model.BranchId.Value : DBNull.Value
        });
        insert.Parameters.AddWithValue(model.RecipientSubject); insert.Parameters.AddWithValue(model.Title);
        insert.Parameters.AddWithValue(model.Body); insert.Parameters.AddWithValue(model.Severity.ToString().ToLowerInvariant());
        Row? created = null;
        await using (var reader = await insert.ExecuteReaderAsync(ct)) if (await reader.ReadAsync(ct)) created = Read(reader);
        if (created is null)
        {
            created = await ReadByOperation(connection, transaction, model.OrganizationId, command.OperationId, ct)
                ?? throw new NotificationUnavailableException();
            if (created.Recipient != model.RecipientSubject || created.Title != model.Title || created.Body != model.Body)
                throw new NotificationConflictException();
        }
        await transaction.CommitAsync(ct);
        return new(ToResponse(created), created.Id == model.Id);
    }
    public async Task<NotificationPage> ListMineAsync(NotificationIdentity identity, Guid organizationId,
        int pageSize, Guid? after, bool unreadOnly, CancellationToken ct)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty)
            throw new ArgumentException("Notification query is invalid.");
        var data = source ?? throw new NotificationUnavailableException();
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await Demand(connection, transaction, organizationId, null, identity, "notifications.view", ct);
        await using var query = new NpgsqlCommand("""
            SELECT notification_id,branch_id,recipient_subject,title,body,severity,is_read,created_at,read_at
            FROM notifications.inbox
            WHERE organization_id=$1 AND recipient_subject=$2
              AND ($3::uuid IS NULL OR notification_id>$3) AND (NOT $4 OR NOT is_read)
            ORDER BY notification_id LIMIT $5
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(identity.Subject);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = after.HasValue ? after.Value : DBNull.Value });
        query.Parameters.AddWithValue(unreadOnly); query.Parameters.AddWithValue(pageSize + 1);
        var rows = new List<Row>();
        await using (var reader = await query.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) rows.Add(Read(reader));
        Guid? next = null; if (rows.Count > pageSize) { rows.RemoveAt(pageSize); next = rows[^1].Id; }
        await transaction.CommitAsync(ct); return new([.. rows.Select(ToResponse)], next);
    }
    public async Task<NotificationResponse> MarkReadAsync(NotificationIdentity identity, Guid organizationId,
        Guid notificationId, CancellationToken ct)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || notificationId == Guid.Empty) throw new ArgumentException("Notification mutation is invalid.");
        var data = source ?? throw new NotificationUnavailableException();
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await Demand(connection, transaction, organizationId, null, identity, "notifications.view", ct);
        await using var update = new NpgsqlCommand("""
            UPDATE notifications.inbox SET is_read=true,read_at=COALESCE(read_at,statement_timestamp())
            WHERE organization_id=$1 AND notification_id=$2 AND recipient_subject=$3
            RETURNING notification_id,branch_id,recipient_subject,title,body,severity,is_read,created_at,read_at
            """, connection, transaction);
        update.Parameters.AddWithValue(organizationId); update.Parameters.AddWithValue(notificationId); update.Parameters.AddWithValue(identity.Subject);
        await using var reader = await update.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new NotificationNotFoundException();
        var row = Read(reader); await transaction.CommitAsync(ct); return ToResponse(row);
    }

    public async Task<NotificationPreferencesResponse> ReadPreferencesAsync(NotificationIdentity identity,
        Guid organizationId, CancellationToken ct) => await Preferences(identity, organizationId, null, ct);
    public async Task<NotificationPreferencesResponse> UpdatePreferencesAsync(NotificationIdentity identity,
        Guid organizationId, UpdateNotificationPreferencesRequest request, CancellationToken ct) =>
        await Preferences(identity, organizationId, request, ct);

    private async Task<NotificationPreferencesResponse> Preferences(NotificationIdentity identity, Guid organizationId,
        UpdateNotificationPreferencesRequest? request, CancellationToken ct)
    {
        identity.Validate(); if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        var data = source ?? throw new NotificationUnavailableException();
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await Demand(connection, transaction, organizationId, null, identity, "notifications.view", ct);
        if (request is not null)
        {
            await using var upsert = new NpgsqlCommand("""
                INSERT INTO notifications.preferences(organization_id,issuer,subject,in_app_enabled,email_enabled,push_enabled)
                VALUES($1,$2,$3,$4,$5,$6)
                ON CONFLICT(organization_id,issuer,subject) DO UPDATE SET in_app_enabled=EXCLUDED.in_app_enabled,
                  email_enabled=EXCLUDED.email_enabled,push_enabled=EXCLUDED.push_enabled,updated_at=statement_timestamp()
                """, connection, transaction);
            upsert.Parameters.AddWithValue(organizationId); upsert.Parameters.AddWithValue(identity.Issuer); upsert.Parameters.AddWithValue(identity.Subject);
            upsert.Parameters.AddWithValue(request.InAppEnabled); upsert.Parameters.AddWithValue(request.EmailEnabled); upsert.Parameters.AddWithValue(request.PushEnabled);
            await upsert.ExecuteNonQueryAsync(ct);
        }
        await using var query = new NpgsqlCommand("""
            SELECT in_app_enabled,email_enabled,push_enabled,updated_at FROM notifications.preferences
            WHERE organization_id=$1 AND issuer=$2 AND subject=$3
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(identity.Issuer); query.Parameters.AddWithValue(identity.Subject);
        await using var reader = await query.ExecuteReaderAsync(ct);
        NotificationPreferencesResponse result;
        if (await reader.ReadAsync(ct)) result = new(reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2), reader.GetFieldValue<DateTimeOffset>(3));
        else result = new(true, false, false, DateTimeOffset.UnixEpoch);
        await transaction.CommitAsync(ct); return result;
    }

    private static async Task<Row?> ReadByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid operationId, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("""
            SELECT notification_id,branch_id,recipient_subject,title,body,severity,is_read,created_at,read_at
            FROM notifications.inbox WHERE organization_id=$1 AND operation_id=$2
            """, c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(operationId);
        await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Read(r) : null;
    }

    private static NotificationResponse ToResponse(Row r) => new(r.Id, r.BranchId, r.Recipient, r.Title, r.Body, r.Severity, r.IsRead, r.CreatedAt, r.ReadAt);
    private static Row Read(NpgsqlDataReader r) => new(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetBoolean(6), r.GetFieldValue<DateTimeOffset>(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8));
    private static async Task EnsureBranch(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid branchId, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT FROM organization.branches WHERE organization_id=$1 AND branch_id=$2 AND is_active)", c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(branchId);
        if (await q.ExecuteScalarAsync(ct) is not true) throw new NotificationConflictException();
    }

    private static async Task Prepare(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, NotificationIdentity identity, CancellationToken ct)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime' AND EXISTS(SELECT FROM pg_class x JOIN pg_namespace n ON n.oid=x.relnamespace
              WHERE n.nspname='notifications' AND x.relname='inbox' AND x.relrowsecurity AND x.relforcerowsecurity
                AND x.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, c, t);
        if (await safety.ExecuteScalarAsync(ct) is not true) throw new NotificationUnavailableException();
        await using var context = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t);
        context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer); context.Parameters.AddWithValue(identity.Subject);
        await context.ExecuteNonQueryAsync(ct);
    }

    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid? branchId, NotificationIdentity identity, string permission, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g
              ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active
                AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission=$4 AND (g.scope_kind='organization' OR ($5::uuid IS NOT NULL AND g.scope_kind='branch' AND g.branch_id=$5)))
            """, c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject);
        q.Parameters.AddWithValue(permission);
        q.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = branchId.HasValue ? branchId.Value : DBNull.Value });
        if (await q.ExecuteScalarAsync(ct) is not true) throw new NotificationDeniedException();
    }
}
