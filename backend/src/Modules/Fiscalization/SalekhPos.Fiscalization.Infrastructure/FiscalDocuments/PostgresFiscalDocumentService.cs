using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Fiscalization.Application.FiscalDocuments;
using SalekhPos.Fiscalization.Contracts.FiscalDocuments;
using SalekhPos.Fiscalization.Domain.FiscalDocuments;

namespace SalekhPos.Fiscalization.Infrastructure.FiscalDocuments;

public sealed class PostgresFiscalDocumentService(NpgsqlDataSource? source, IFiscalProviderRegistry providers)
    : IFiscalDocumentService
{
    private sealed record DocumentRow(Guid Id, Guid BranchId, Guid SaleId, string ProviderKey,
        string DocumentType, string Currency, decimal Gross, string Payload, string Hash, string Status,
        string? Reference, int AttemptCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    public Task<FiscalSubmissionResponse> SubmitAsync(FiscalIdentity identity, Guid organizationId, Guid branchId,
        SubmitFiscalDocumentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var draft = new FiscalDocumentDraft(organizationId, branchId, request.DocumentId, request.SaleId,
            request.ProviderKey, request.DocumentType, request.Currency, request.GrossAmount, request.Payload);
        return ExecuteAsync(identity, draft, false, ct);
    }

    public async Task<FiscalSubmissionResponse> RetryAsync(FiscalIdentity identity, Guid organizationId,
        Guid documentId, CancellationToken ct)
    {
        Validate(identity, organizationId, documentId);
        var dataSource = source ?? throw new FiscalUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await Demand(connection, transaction, organizationId, null, identity, "fiscalization.submit", ct);
        var existing = await ReadDocument(connection, transaction, organizationId, documentId, true, ct)
            ?? throw new FiscalNotFoundException();
        if (existing.Status is "accepted" or "rejected") throw new FiscalNotRetryableException();
        var draft = new FiscalDocumentDraft(organizationId, existing.BranchId, existing.Id, existing.SaleId,
            existing.ProviderKey, existing.DocumentType, existing.Currency, existing.Gross, existing.Payload);
        return await SubmitLocked(connection, transaction, identity, draft, existing, false, ct);
    }

    public async Task<FiscalDocumentResponse?> ReadAsync(FiscalIdentity identity, Guid organizationId,
        Guid documentId, CancellationToken ct)
    {
        Validate(identity, organizationId, documentId);
        var dataSource = source ?? throw new FiscalUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await Demand(connection, transaction, organizationId, null, identity, "fiscalization.view", ct);
        var row = await ReadDocument(connection, transaction, organizationId, documentId, false, ct);
        if (row is null) { await transaction.CommitAsync(ct); return null; }
        var response = await ToResponse(connection, transaction, organizationId, row, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    private async Task<FiscalSubmissionResponse> ExecuteAsync(FiscalIdentity identity, FiscalDocumentDraft draft,
        bool retry, CancellationToken ct)
    {
        identity.Validate();
        var dataSource = source ?? throw new FiscalUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await Prepare(connection, transaction, draft.OrganizationId, identity, ct);
        await Demand(connection, transaction, draft.OrganizationId, draft.BranchId, identity, "fiscalization.submit", ct);
        var existing = await ReadDocument(connection, transaction, draft.OrganizationId, draft.DocumentId, true, ct);
        if (existing is not null && !Equivalent(existing, draft)) throw new FiscalConflictException();
        if (existing is not null && existing.Status is "accepted" or "rejected")
        {
            var replay = await ToResponse(connection, transaction, draft.OrganizationId, existing, ct);
            await transaction.CommitAsync(ct);
            return new(replay, false, false);
        }
        return await SubmitLocked(connection, transaction, identity, draft, existing, retry, ct);
    }

    private async Task<FiscalSubmissionResponse> SubmitLocked(NpgsqlConnection connection, NpgsqlTransaction transaction,
        FiscalIdentity identity, FiscalDocumentDraft draft, DocumentRow? existing, bool retry, CancellationToken ct)
    {
        await using (var mutex = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction))
        { mutex.Parameters.AddWithValue($"{draft.OrganizationId:D}:{draft.DocumentId:D}"); await mutex.ExecuteNonQueryAsync(ct); }
        existing = await ReadDocument(connection, transaction, draft.OrganizationId, draft.DocumentId, true, ct) ?? existing;
        if (existing is not null && !Equivalent(existing, draft)) throw new FiscalConflictException();
        if (existing is not null && existing.Status is "accepted" or "rejected")
        {
            var replay = await ToResponse(connection, transaction, draft.OrganizationId, existing, ct);
            await transaction.CommitAsync(ct); return new(replay, false, false);
        }
        var created = existing is null;
        if (created) existing = await InsertDocument(connection, transaction, draft, ct);
        var provider = providers.Resolve(draft.ProviderKey);
        FiscalProviderResult result;
        try
        {
            result = await provider.SubmitAsync(new(draft.OrganizationId, draft.BranchId, draft.DocumentId,
                draft.SaleId, draft.DocumentType, draft.Currency, draft.GrossAmount, draft.Payload,
                draft.PayloadSha256), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            result = FiscalProviderResult.Retry("provider_exception", Limit(error.Message, 1000), null);
        }
        var attemptedAt = await DatabaseTime(connection, transaction, ct);
        var attempt = checked(existing!.AttemptCount + 1);
        var outcome = result.Accepted ? "accepted" : result.Retryable ? "retryable" : "rejected";
        await InsertAttempt(connection, transaction, draft, attempt, outcome, result, attemptedAt, ct);
        var status = result.Accepted ? "accepted" : result.Retryable ? "submitted" : "rejected";
        await UpdateDocument(connection, transaction, draft, status, result.ProviderReference, attempt, attemptedAt, ct);
        var updated = existing with { Status = status, Reference = result.ProviderReference,
            AttemptCount = attempt, UpdatedAt = attemptedAt };
        var response = await ToResponse(connection, transaction, draft.OrganizationId, updated, ct);
        await transaction.CommitAsync(ct);
        return new(response, created, true);
    }

    private static async Task<DocumentRow> InsertDocument(NpgsqlConnection connection, NpgsqlTransaction transaction,
        FiscalDocumentDraft draft, CancellationToken ct)
    {
        var now = await DatabaseTime(connection, transaction, ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO fiscalization.documents(organization_id,document_id,branch_id,sale_id,provider_key,
              document_type,currency,gross_amount,payload,payload_sha256,status,created_at,updated_at)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'pending',$11,$11)
            """, connection, transaction);
        command.Parameters.AddWithValue(draft.OrganizationId); command.Parameters.AddWithValue(draft.DocumentId);
        command.Parameters.AddWithValue(draft.BranchId); command.Parameters.AddWithValue(draft.SaleId);
        command.Parameters.AddWithValue(draft.ProviderKey); command.Parameters.AddWithValue(draft.DocumentType);
        command.Parameters.AddWithValue(draft.Currency); command.Parameters.AddWithValue(draft.GrossAmount);
        command.Parameters.AddWithValue(draft.Payload); command.Parameters.AddWithValue(draft.PayloadSha256);
        command.Parameters.AddWithValue(now); await command.ExecuteNonQueryAsync(ct);
        return new(draft.DocumentId, draft.BranchId, draft.SaleId, draft.ProviderKey, draft.DocumentType,
            draft.Currency, draft.GrossAmount, draft.Payload, draft.PayloadSha256, "pending", null, 0, now, now);
    }

    private static async Task<DocumentRow?> ReadDocument(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid documentId, bool locked, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT document_id,branch_id,sale_id,provider_key,document_type,currency,gross_amount,payload,
              payload_sha256,status,provider_reference,attempt_count,created_at,updated_at
            FROM fiscalization.documents WHERE organization_id=$1 AND document_id=$2{(locked ? " FOR UPDATE" : "")}
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(documentId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetDecimal(6), reader.GetString(7),
            reader.GetString(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.GetInt32(11), reader.GetFieldValue<DateTimeOffset>(12), reader.GetFieldValue<DateTimeOffset>(13)) : null;
    }

    private static async Task InsertAttempt(NpgsqlConnection connection, NpgsqlTransaction transaction,
        FiscalDocumentDraft draft, int attempt, string outcome, FiscalProviderResult result,
        DateTimeOffset attemptedAt, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO fiscalization.submission_attempts(organization_id,document_id,attempt_number,outcome,
              provider_reference,provider_code,failure_reason,attempted_at,retry_after)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
            """, connection, transaction);
        command.Parameters.AddWithValue(draft.OrganizationId); command.Parameters.AddWithValue(draft.DocumentId);
        command.Parameters.AddWithValue(attempt); command.Parameters.AddWithValue(outcome);
        AddNullable(command, NpgsqlDbType.Varchar, result.ProviderReference);
        AddNullable(command, NpgsqlDbType.Varchar, result.ProviderCode);
        AddNullable(command, NpgsqlDbType.Varchar, result.FailureReason);
        command.Parameters.AddWithValue(attemptedAt); AddNullable(command, NpgsqlDbType.TimestampTz, result.RetryAfter);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpdateDocument(NpgsqlConnection connection, NpgsqlTransaction transaction,
        FiscalDocumentDraft draft, string status, string? reference, int attempt, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE fiscalization.documents SET status=$3,provider_reference=$4,attempt_count=$5,updated_at=$6
            WHERE organization_id=$1 AND document_id=$2
            """, connection, transaction);
        command.Parameters.AddWithValue(draft.OrganizationId); command.Parameters.AddWithValue(draft.DocumentId);
        command.Parameters.AddWithValue(status); AddNullable(command, NpgsqlDbType.Varchar, reference);
        command.Parameters.AddWithValue(attempt); command.Parameters.AddWithValue(now);
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new FiscalUnavailableException();
    }

    private static async Task<FiscalDocumentResponse> ToResponse(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, DocumentRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT attempt_number,outcome,provider_reference,provider_code,failure_reason,attempted_at,retry_after
            FROM fiscalization.submission_attempts WHERE organization_id=$1 AND document_id=$2 ORDER BY attempt_number
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(row.Id);
        var attempts = new List<FiscalAttemptResponse>();
        await using (var reader = await command.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct))
            attempts.Add(new(reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
        return new(row.Id, row.BranchId, row.SaleId, row.ProviderKey, row.DocumentType, row.Currency, row.Gross,
            row.Hash, row.Status, row.Reference, row.AttemptCount, row.CreatedAt, row.UpdatedAt, attempts);
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, FiscalIdentity identity, CancellationToken ct)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime' AND (SELECT count(*)=2 FROM pg_class c
              JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='fiscalization'
              AND c.relname IN('documents','submission_attempts') AND c.relrowsecurity AND c.relforcerowsecurity
              AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(ct) is not true) throw new FiscalUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(ct);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId,
        Guid? branchId, FiscalIdentity identity, string permission, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(branchId.HasValue ? BranchDemandSql : OrganizationDemandSql,
            connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject); command.Parameters.AddWithValue(permission);
        if (branchId.HasValue) command.Parameters.AddWithValue(branchId.Value);
        if (await command.ExecuteScalarAsync(ct) is not true) throw new FiscalDeniedException();
    }

    private static async Task<DateTimeOffset> DatabaseTime(NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT statement_timestamp()", connection, transaction);
        return await command.ExecuteScalarAsync(ct) switch
        { DateTimeOffset value => value, DateTime value when value.Kind == DateTimeKind.Utc => new(value), _ => throw new FiscalUnavailableException() };
    }

    private static bool Equivalent(DocumentRow row, FiscalDocumentDraft draft) => row.Id == draft.DocumentId
        && row.BranchId == draft.BranchId && row.SaleId == draft.SaleId && row.ProviderKey == draft.ProviderKey
        && row.DocumentType == draft.DocumentType && row.Currency == draft.Currency && row.Gross == draft.GrossAmount
        && row.Hash == draft.PayloadSha256 && row.Payload == draft.Payload;

    private static void Validate(FiscalIdentity identity, Guid organizationId, Guid documentId)
    { ArgumentNullException.ThrowIfNull(identity); identity.Validate(); if (organizationId == Guid.Empty || documentId == Guid.Empty) throw new ArgumentException("Fiscal query is invalid."); }
    private static string Limit(string value, int max) => string.IsNullOrWhiteSpace(value) ? "Provider call failed." : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static void AddNullable(NpgsqlCommand command, NpgsqlDbType type, object? value) =>
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = type, Value = value ?? DBNull.Value });

    private const string OrganizationDemandSql = """
        SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g
          ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
          JOIN organization.organizations o ON o.organization_id=m.organization_id
          WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active AND o.is_active
          AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
          AND g.permission=$4 AND g.scope_kind='organization')
        """;
    private const string BranchDemandSql = """
        SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g
          ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
          JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$5
          JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id
          JOIN organization.organizations o ON o.organization_id=b.organization_id
          WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active AND b.is_active AND z.is_active AND o.is_active
          AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
          AND g.permission=$4 AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
            OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
            OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
        """;
}
