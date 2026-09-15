using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Promotions.Application.Campaigns;
using SalekhPos.Promotions.Contracts.Campaigns;
using SalekhPos.Promotions.Domain.Promotions;

namespace SalekhPos.Promotions.Infrastructure.Campaigns;

public sealed class PostgresPromotionService(NpgsqlDataSource? source) : IPromotionService
{
    private sealed record Row(Guid Id, string Code, string Name, Guid? BranchId, string Kind,
        decimal Value, string? Currency, decimal MinimumSubtotal, DateTimeOffset StartsAt,
        DateTimeOffset? EndsAt, bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    public async Task<PromotionWriteResult> CreateAsync(PromotionIdentity identity,
        CreatePromotionCommand command, CancellationToken ct)
    {
        identity.Validate();
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
        var promotion = command.ToPromotion();
        var dataSource = source ?? throw new PromotionsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await Prepare(connection, transaction, promotion.OrganizationId, identity, ct);
        await Demand(connection, transaction, promotion.OrganizationId, promotion.BranchId, identity, "pricing.manage", ct);
        if (promotion.BranchId.HasValue) await EnsureBranch(connection, transaction, promotion.OrganizationId, promotion.BranchId.Value, ct);
        var created = await Insert(connection, transaction, promotion, command.OperationId, identity, ct);
        if (created is not null)
        {
            await transaction.CommitAsync(ct);
            return new(ToResponse(created), true);
        }
        var replay = await ReadByOperation(connection, transaction, promotion.OrganizationId, command.OperationId, ct)
            ?? throw new PromotionsUnavailableException();
        if (!Equivalent(promotion, replay)) throw new PromotionConflictException();
        await transaction.CommitAsync(ct);
        return new(ToResponse(replay), false);
    }
    public async Task<PromotionPage> ListAsync(PromotionIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken ct)
    {
        ValidateQuery(identity, organizationId, branchId, pageSize, after);
        var dataSource = source ?? throw new PromotionsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await Demand(connection, transaction, organizationId, branchId, identity, "pricing.view", ct);
        await using var query = new NpgsqlCommand($"""
            {SelectColumns}
            WHERE organization_id=$1 AND (branch_id IS NULL OR branch_id=$2)
              AND ($3::uuid IS NULL OR promotion_id>$3)
            ORDER BY promotion_id LIMIT $4
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(branchId);
        query.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = after.HasValue ? after.Value : DBNull.Value
        });
        query.Parameters.AddWithValue(pageSize + 1);
        var rows = new List<Row>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) rows.Add(Read(reader));
        Guid? next = null;
        if (rows.Count > pageSize)
        {
            rows.RemoveAt(pageSize);
            next = rows[^1].Id;
        }
        await transaction.CommitAsync(ct);
        return new(rows.Select(ToResponse).ToArray(), next);
    }
    public async Task<PromotionResponse?> ReadAsync(PromotionIdentity identity, Guid organizationId,
        Guid branchId, Guid promotionId, CancellationToken ct)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || branchId == Guid.Empty || promotionId == Guid.Empty)
            throw new ArgumentException("Promotion query is invalid.");
        var dataSource = source ?? throw new PromotionsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await Demand(connection, transaction, organizationId, branchId, identity, "pricing.view", ct);
        var row = await ReadById(connection, transaction, organizationId, branchId, promotionId, false, ct);
        await transaction.CommitAsync(ct);
        return row is null ? null : ToResponse(row);
    }

    public async Task<PromotionResponse> DeactivateAsync(PromotionIdentity identity, Guid organizationId,
        Guid branchId, Guid promotionId, long expectedVersion, CancellationToken ct)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || branchId == Guid.Empty || promotionId == Guid.Empty || expectedVersion < 1)
            throw new ArgumentException("Promotion mutation is invalid.");
        var dataSource = source ?? throw new PromotionsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await Demand(connection, transaction, organizationId, branchId, identity, "pricing.manage", ct);
        var row = await ReadById(connection, transaction, organizationId, branchId, promotionId, true, ct)
            ?? throw new PromotionNotFoundException();
        if (row.Version != expectedVersion || !row.IsActive) throw new PromotionConflictException();
        await using var update = new NpgsqlCommand("""
            UPDATE promotions.promotions
            SET is_active=false,row_version=row_version+1,updated_at=statement_timestamp()
            WHERE organization_id=$1 AND promotion_id=$2 AND row_version=$3
            """, connection, transaction);
        update.Parameters.AddWithValue(organizationId);
        update.Parameters.AddWithValue(promotionId);
        update.Parameters.AddWithValue(expectedVersion);
        if (await update.ExecuteNonQueryAsync(ct) != 1) throw new PromotionConflictException();
        var changed = await ReadById(connection, transaction, organizationId, branchId, promotionId, false, ct)
            ?? throw new PromotionsUnavailableException();
        await transaction.CommitAsync(ct);
        return ToResponse(changed);
    }
    public async Task<PromotionEvaluationResponse> EvaluateAsync(PromotionIdentity identity, Guid organizationId,
        Guid branchId, decimal subtotal, string currency, DateTimeOffset at, CancellationToken ct)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || branchId == Guid.Empty || subtotal < 0
            || decimal.Round(subtotal, 6) != subtotal || at == default || at.Offset != TimeSpan.Zero)
            throw new ArgumentException("Promotion evaluation is invalid.");
        var normalizedCurrency = currency?.Trim().ToUpperInvariant() ?? "";
        if (normalizedCurrency.Length != 3 || normalizedCurrency.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Promotion currency is invalid.");
        var dataSource = source ?? throw new PromotionsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await Demand(connection, transaction, organizationId, branchId, identity, "pricing.view", ct);
        await using var query = new NpgsqlCommand($"""
            {SelectColumns}
            WHERE organization_id=$1 AND is_active AND (branch_id IS NULL OR branch_id=$2)
              AND starts_at<=$3 AND (ends_at IS NULL OR ends_at>$3)
            ORDER BY branch_id IS NOT NULL DESC,promotion_id
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(at);
        var rows = new List<Row>();
        await using (var reader = await query.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) rows.Add(Read(reader));
        AppliedPromotionResponse? chosen = null;
        foreach (var row in rows)
        {
            var model = ToDomain(organizationId, row);
            var amount = model.DiscountFor(subtotal, normalizedCurrency, at);
            if (amount <= 0) continue;
            if (chosen is null || amount > chosen.Discount)
                chosen = new(row.Id, row.Code, row.Name, amount);
        }
        await transaction.CommitAsync(ct);
        var total = chosen?.Discount ?? 0m;
        var applied = chosen is null
            ? Array.Empty<AppliedPromotionResponse>()
            : new[] { chosen };
        return new(subtotal, total, subtotal - total, normalizedCurrency, applied);
    }

    private static Promotion ToDomain(Guid organizationId, Row row) => new(organizationId, row.Id,
        row.Code, row.Name, row.BranchId, row.Kind == "percentage" ? PromotionDiscountKind.Percentage
            : PromotionDiscountKind.FixedAmount, row.Value, row.Currency, row.MinimumSubtotal,
        row.StartsAt, row.EndsAt, row.IsActive);
    private static PromotionResponse ToResponse(Row row) => new(row.Id, row.Code, row.Name, row.BranchId,
        row.Kind, row.Value, row.Currency, row.MinimumSubtotal, row.StartsAt, row.EndsAt,
        row.IsActive, row.Version, row.CreatedAt, row.UpdatedAt);

    private static async Task<Row?> Insert(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Promotion promotion, Guid operationId, PromotionIdentity identity, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO promotions.promotions(organization_id,promotion_id,operation_id,code,name,branch_id,
              discount_kind,discount_value,currency,minimum_subtotal,starts_at,ends_at,is_active,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,true,$13,$14)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING promotion_id,code,name,branch_id,discount_kind,discount_value,currency,minimum_subtotal,
              starts_at,ends_at,is_active,row_version,created_at,updated_at
            """, connection, transaction);
        command.Parameters.AddWithValue(promotion.OrganizationId); command.Parameters.AddWithValue(promotion.Id);
        command.Parameters.AddWithValue(operationId); command.Parameters.AddWithValue(promotion.Code);
        command.Parameters.AddWithValue(promotion.Name);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Uuid,
            Value=promotion.BranchId.HasValue ? promotion.BranchId.Value : DBNull.Value });
        command.Parameters.AddWithValue(promotion.Kind == PromotionDiscountKind.Percentage ? "percentage" : "fixed");
        command.Parameters.AddWithValue(promotion.Value);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Char,
            Value=(object?)promotion.Currency ?? DBNull.Value });
        command.Parameters.AddWithValue(promotion.MinimumSubtotal); command.Parameters.AddWithValue(promotion.StartsAt);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.TimestampTz,
            Value=promotion.EndsAt.HasValue ? promotion.EndsAt.Value : DBNull.Value });
        command.Parameters.AddWithValue(identity.Issuer); command.Parameters.AddWithValue(identity.Subject);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    private static async Task<Row?> ReadByOperation(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid operationId, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand($"""
            {SelectColumns} WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(operationId);
        await using var reader = await query.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }
    private static async Task<Row?> ReadById(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, Guid promotionId, bool forUpdate, CancellationToken ct)
    {
        var suffix = forUpdate ? " FOR UPDATE" : "";
        await using var query = new NpgsqlCommand($"""
            {SelectColumns}
            WHERE organization_id=$1 AND promotion_id=$2 AND (branch_id IS NULL OR branch_id=$3){suffix}
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(promotionId);
        query.Parameters.AddWithValue(branchId);
        await using var reader = await query.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    private static async Task EnsureBranch(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM organization.branches b JOIN organization.businesses z
              ON z.organization_id=b.organization_id AND z.business_id=b.business_id
              WHERE b.organization_id=$1 AND b.branch_id=$2 AND b.is_active AND z.is_active)
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        if (await query.ExecuteScalarAsync(ct) is not true) throw new PromotionConflictException();
    }
    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, PromotionIdentity identity, CancellationToken ct)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='promotions' AND c.relname='promotions' AND c.relrowsecurity
                  AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(ct) is not true) throw new PromotionsUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(ct);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid? branchId, PromotionIdentity identity, string permission, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m
              JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active
                AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission=$4 AND (g.scope_kind='organization' OR $5::uuid IS NOT NULL AND g.scope_kind='branch' AND g.branch_id=$5))
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(identity.Issuer);
        query.Parameters.AddWithValue(identity.Subject); query.Parameters.AddWithValue(permission);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Uuid,
            Value=branchId.HasValue ? branchId.Value : DBNull.Value });
        if (await query.ExecuteScalarAsync(ct) is not true) throw new PromotionsDeniedException();
    }

    private static Row Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3),
        reader.GetString(4), reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.GetDecimal(7), reader.GetFieldValue<DateTimeOffset>(8),
        reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9), reader.GetBoolean(10),
        reader.GetInt64(11), reader.GetFieldValue<DateTimeOffset>(12), reader.GetFieldValue<DateTimeOffset>(13));

    private static bool Equivalent(Promotion promotion, Row row) => row.Id == promotion.Id
        && row.Code == promotion.Code && row.Name == promotion.Name && row.BranchId == promotion.BranchId
        && row.Kind == (promotion.Kind == PromotionDiscountKind.Percentage ? "percentage" : "fixed")
        && row.Value == promotion.Value && row.Currency == promotion.Currency
        && row.MinimumSubtotal == promotion.MinimumSubtotal && row.StartsAt == promotion.StartsAt
        && row.EndsAt == promotion.EndsAt;

    private static void ValidateQuery(PromotionIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || branchId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty)
            throw new ArgumentException("Promotion query is invalid.");
    }
    private const string SelectColumns =
        "SELECT promotion_id,code,name,branch_id,discount_kind,discount_value,currency,minimum_subtotal," +
        " starts_at,ends_at,is_active,row_version,created_at,updated_at FROM promotions.promotions";
}
