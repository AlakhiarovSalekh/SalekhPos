using System.Buffers.Binary;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Audit.Application.AuditTrail;
using SalekhPos.Audit.Contracts.AuditTrail;
using SalekhPos.Audit.Domain.AuditEvents;

namespace SalekhPos.Audit.Infrastructure.AuditTrail;

public sealed class PostgresAuditTrail(NpgsqlDataSource? source) : IAuditTrail
{
    private sealed record Head(long Sequence, byte[] Hash);
    private sealed record Row(Guid Id, Guid OperationId, long Sequence, string ActorIssuer, string ActorSubject,
        string Action, string TargetType, Guid? TargetId, Guid? BranchId, Guid? DeviceId, string? SourceIp,
        string Outcome, string? Reason, string CorrelationId, string RequestId, DateTimeOffset OccurredAt,
        byte[] PreviousHash, byte[] EventHash);

    public async Task<AuditAppendResult> AppendAsync(AuditIdentity identity, AppendAuditCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(command);
        identity.Validate(); var draft = command.Event ?? throw new ArgumentException("Audit event is required.");
        var dataSource = source ?? throw new AuditUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await Prepare(connection, transaction, draft.OrganizationId, identity, ct);
        await EnsureHead(connection, transaction, draft.OrganizationId, ct);
        var head = await LockHead(connection, transaction, draft.OrganizationId, ct);
        var replay = await ReadByOperation(connection, transaction, draft.OrganizationId, draft.OperationId, ct);
        if (replay is not null)
        {
            if (!Equivalent(replay, identity, draft)) throw new AuditConflictException();
            await transaction.CommitAsync(ct);
            return new(ToResponse(replay), false);
        }

        var occurredAt = await DatabaseTime(connection, transaction, ct);
        var sequence = checked(head.Sequence + 1);
        var outcome = AuditEventDraft.OutcomeName(draft.Outcome);
        var hash = ComputeHash(head.Hash, sequence, identity, draft, outcome, occurredAt);
        var row = new Row(draft.EventId, draft.OperationId, sequence, identity.Issuer, identity.Subject,
            draft.Action, draft.TargetType, draft.TargetId, draft.BranchId, draft.DeviceId, draft.SourceIp,
            outcome, draft.Reason, draft.CorrelationId, draft.RequestId, occurredAt, head.Hash, hash);
        await InsertEvent(connection, transaction, draft.OrganizationId, row, ct);
        await UpdateHead(connection, transaction, draft.OrganizationId, sequence, hash, occurredAt, ct);
        await transaction.CommitAsync(ct);
        return new(ToResponse(row), true);
    }

    public async Task<AuditEventPage> ListAsync(AuditIdentity identity, Guid organizationId, int pageSize,
        long? afterSequence, string? action, Guid? branchId, CancellationToken ct)
    {
        ValidateQuery(identity, organizationId, pageSize, afterSequence, action, branchId);
        var dataSource = source ?? throw new AuditUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await DemandView(connection, transaction, organizationId, branchId, identity, ct);
        await using var query = new NpgsqlCommand($"""
            {SelectColumns}
            WHERE organization_id=$1 AND ($2::bigint IS NULL OR sequence>$2)
              AND ($3::text IS NULL OR action=$3) AND ($4::uuid IS NULL OR branch_id=$4)
            ORDER BY sequence LIMIT $5
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Bigint, Value=afterSequence.HasValue?afterSequence.Value:DBNull.Value });
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Text, Value=(object?)action??DBNull.Value });
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Uuid, Value=branchId.HasValue?branchId.Value:DBNull.Value });
        query.Parameters.AddWithValue(pageSize + 1);
        var rows = new List<Row>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) rows.Add(ReadRow(reader));
        long? next = null;
        if (rows.Count > pageSize) { rows.RemoveAt(pageSize); next = rows[^1].Sequence; }
        await transaction.CommitAsync(ct);
        return new(rows.Select(ToResponse).ToArray(), next);
    }

    public async Task<AuditEventResponse?> ReadAsync(AuditIdentity identity, Guid organizationId,
        Guid eventId, CancellationToken ct)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || eventId == Guid.Empty) throw new ArgumentException("Audit query is invalid.");
        var dataSource = source ?? throw new AuditUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await DemandView(connection, transaction, organizationId, null, identity, ct);
        await using var query = new NpgsqlCommand($"{SelectColumns} WHERE organization_id=$1 AND event_id=$2",
            connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(eventId);
        await using var reader = await query.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) { await transaction.CommitAsync(ct); return null; }
        var row = ReadRow(reader);
        await transaction.CommitAsync(ct);
        return ToResponse(row);
    }

    public async Task<AuditIntegrityResponse> VerifyAsync(AuditIdentity identity, Guid organizationId,
        long? fromSequence, int limit, CancellationToken ct)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || fromSequence is < 1 || limit is < 1 or > 1000)
            throw new ArgumentException("Audit verification query is invalid.");
        var dataSource = source ?? throw new AuditUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await DemandView(connection, transaction, organizationId, null, identity, ct);
        var start = fromSequence ?? 1;
        await using var query = new NpgsqlCommand($"""
            {SelectColumns} WHERE organization_id=$1 AND sequence>=$2 ORDER BY sequence LIMIT $3
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(start); query.Parameters.AddWithValue(limit);
        var rows = new List<Row>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) rows.Add(ReadRow(reader));
        await transaction.CommitAsync(ct);
        if (rows.Count == 0) return new(true, 0, null, null, null);
        var valid = true;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (index > 0 && (row.Sequence != rows[index - 1].Sequence + 1
                || !CryptographicOperations.FixedTimeEquals(row.PreviousHash, rows[index - 1].EventHash))) valid = false;
            var draft = new AuditEventDraft(organizationId, row.Id, row.OperationId, row.Action, row.TargetType,
                row.TargetId, row.BranchId, row.DeviceId, row.SourceIp, row.CorrelationId, row.RequestId,
                ParseOutcome(row.Outcome), row.Reason);
            var expected = ComputeHash(row.PreviousHash, row.Sequence,
                new(row.ActorIssuer, row.ActorSubject), draft, row.Outcome, row.OccurredAt);
            if (!CryptographicOperations.FixedTimeEquals(expected, row.EventHash)) valid = false;
        }
        return new(valid, rows.Count, rows[0].Sequence, rows[^1].Sequence, Hex(rows[^1].EventHash));
    }

    private static async Task EnsureHead(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO audit.stream_heads(organization_id,last_sequence,last_hash)
            VALUES($1,0,decode(repeat('00',32),'hex')) ON CONFLICT(organization_id) DO NOTHING
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Head> LockHead(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT last_sequence,last_hash FROM audit.stream_heads WHERE organization_id=$1 FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new AuditUnavailableException();
        return new(reader.GetInt64(0), reader.GetFieldValue<byte[]>(1));
    }

    private static async Task<Row?> ReadByOperation(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid operationId, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand($"{SelectColumns} WHERE organization_id=$1 AND operation_id=$2",
            connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(operationId);
        await using var reader = await query.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRow(reader) : null;
    }

    private static async Task InsertEvent(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Row row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO audit.events(organization_id,event_id,operation_id,sequence,actor_issuer,actor_subject,
              action,target_type,target_id,branch_id,device_id,source_ip,outcome,reason,correlation_id,request_id,
              occurred_at,previous_hash,event_hash)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19)
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(row.Id);
        command.Parameters.AddWithValue(row.OperationId); command.Parameters.AddWithValue(row.Sequence);
        command.Parameters.AddWithValue(row.ActorIssuer); command.Parameters.AddWithValue(row.ActorSubject);
        command.Parameters.AddWithValue(row.Action); command.Parameters.AddWithValue(row.TargetType);
        AddNullable(command, NpgsqlDbType.Uuid, row.TargetId); AddNullable(command, NpgsqlDbType.Uuid, row.BranchId);
        AddNullable(command, NpgsqlDbType.Uuid, row.DeviceId); AddNullable(command, NpgsqlDbType.Varchar, row.SourceIp);
        command.Parameters.AddWithValue(row.Outcome); AddNullable(command, NpgsqlDbType.Varchar, row.Reason);
        command.Parameters.AddWithValue(row.CorrelationId); command.Parameters.AddWithValue(row.RequestId);
        command.Parameters.AddWithValue(row.OccurredAt); command.Parameters.AddWithValue(row.PreviousHash);
        command.Parameters.AddWithValue(row.EventHash);
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new AuditUnavailableException();
    }

    private static async Task UpdateHead(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, long sequence, byte[] hash, DateTimeOffset occurredAt, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE audit.stream_heads SET last_sequence=$2,last_hash=$3,updated_at=$4 WHERE organization_id=$1
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(sequence);
        command.Parameters.AddWithValue(hash); command.Parameters.AddWithValue(occurredAt);
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new AuditUnavailableException();
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, AuditIdentity identity, CancellationToken ct)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND (SELECT count(*)=2 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='audit' AND c.relname IN('events','stream_heads') AND c.relrowsecurity
                  AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(ct) is not true) throw new AuditUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(ct);
    }

    private static async Task DemandView(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid? branchId, AuditIdentity identity, CancellationToken ct)
    {
        var sql = branchId.HasValue ? BranchDemandSql : OrganizationDemandSql;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
        if (branchId.HasValue) command.Parameters.AddWithValue(branchId.Value);
        if (await command.ExecuteScalarAsync(ct) is not true) throw new AuditDeniedException();
    }

    private static async Task<DateTimeOffset> DatabaseTime(NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT statement_timestamp()", connection, transaction);
        var value = await command.ExecuteScalarAsync(ct);
        return value is DateTimeOffset timestamp ? timestamp : throw new AuditUnavailableException();
    }

    private static byte[] ComputeHash(byte[] previousHash, long sequence, AuditIdentity identity,
        AuditEventDraft draft, string outcome, DateTimeOffset occurredAt)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(previousHash);
        Append(hash, sequence.ToString(CultureInfo.InvariantCulture));
        Append(hash, identity.Issuer); Append(hash, identity.Subject); Append(hash, draft.Action);
        Append(hash, draft.TargetType); Append(hash, draft.TargetId?.ToString("D") ?? "");
        Append(hash, draft.BranchId?.ToString("D") ?? ""); Append(hash, draft.DeviceId?.ToString("D") ?? "");
        Append(hash, draft.SourceIp ?? ""); Append(hash, outcome); Append(hash, draft.Reason ?? "");
        Append(hash, draft.CorrelationId); Append(hash, draft.RequestId);
        Append(hash, occurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Append(hash, draft.EventId.ToString("D")); Append(hash, draft.OperationId.ToString("D"));
        return hash.GetHashAndReset();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var data = Encoding.UTF8.GetBytes(value); Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length); hash.AppendData(length); hash.AppendData(data);
    }

    private static Row ReadRow(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetString(3), reader.GetString(4),
        reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetGuid(7),
        reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.IsDBNull(9) ? null : reader.GetGuid(9),
        reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetString(11),
        reader.IsDBNull(12) ? null : reader.GetString(12), reader.GetString(13), reader.GetString(14),
        reader.GetFieldValue<DateTimeOffset>(15), reader.GetFieldValue<byte[]>(16), reader.GetFieldValue<byte[]>(17));

    private static AuditEventResponse ToResponse(Row row) => new(row.Id, row.Sequence, row.ActorSubject,
        row.Action, row.TargetType, row.TargetId, row.BranchId, row.DeviceId, row.SourceIp, row.Outcome,
        row.Reason, row.CorrelationId, row.RequestId, row.OccurredAt, Hex(row.PreviousHash), Hex(row.EventHash));

    private static bool Equivalent(Row row, AuditIdentity identity, AuditEventDraft draft) =>
        row.Id == draft.EventId && row.ActorIssuer == identity.Issuer && row.ActorSubject == identity.Subject
        && row.Action == draft.Action && row.TargetType == draft.TargetType && row.TargetId == draft.TargetId
        && row.BranchId == draft.BranchId && row.DeviceId == draft.DeviceId && row.SourceIp == draft.SourceIp
        && row.Outcome == AuditEventDraft.OutcomeName(draft.Outcome) && row.Reason == draft.Reason
        && row.CorrelationId == draft.CorrelationId && row.RequestId == draft.RequestId;

    private static string Hex(byte[] value) => Convert.ToHexString(value).ToLowerInvariant();
    private static AuditOutcome ParseOutcome(string value) => value switch
    {
        "attempted" => AuditOutcome.Attempted, "succeeded" => AuditOutcome.Succeeded,
        "failed" => AuditOutcome.Failed, _ => throw new AuditUnavailableException()
    };

    private static void ValidateQuery(AuditIdentity identity, Guid organizationId, int pageSize,
        long? afterSequence, string? action, Guid? branchId)
    {
        ArgumentNullException.ThrowIfNull(identity); identity.Validate();
        if (organizationId == Guid.Empty || pageSize is < 1 or > 100 || afterSequence is < 0
            || branchId == Guid.Empty || action is not null && (string.IsNullOrWhiteSpace(action)
                || action != action.Trim() || action.Length > 180 || action.Any(char.IsControl)))
            throw new ArgumentException("Audit query is invalid.");
    }

    private static void AddNullable(NpgsqlCommand command, NpgsqlDbType type, object? value) =>
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = type, Value = value ?? DBNull.Value });

    private const string SelectColumns = """
        SELECT event_id,operation_id,sequence,actor_issuer,actor_subject,action,target_type,target_id,branch_id,
          device_id,source_ip,outcome,reason,correlation_id,request_id,occurred_at,previous_hash,event_hash
        FROM audit.events
        """;

    private const string OrganizationDemandSql = """
        SELECT EXISTS(SELECT FROM access.memberships m
          JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
          JOIN organization.organizations o ON o.organization_id=m.organization_id
          WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active AND o.is_active
            AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
            AND g.permission='audit.view' AND g.scope_kind='organization')
        """;

    private const string BranchDemandSql = """
        SELECT EXISTS(SELECT FROM access.memberships m
          JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
          JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$4
          JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id
          JOIN organization.organizations o ON o.organization_id=b.organization_id
          WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active
            AND b.is_active AND z.is_active AND o.is_active
            AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
            AND g.permission='audit.view'
            AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
              OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
              OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
        """;
}
