using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Pricing.Application.Prices;
using SalekhPos.Pricing.Contracts.Prices;
using SalekhPos.Pricing.Domain.Prices;

namespace SalekhPos.Pricing.Infrastructure.Prices;

public sealed class PostgresPriceBook(NpgsqlDataSource? source) : IPriceBook
{
    public async Task<PriceWriteResult> ScheduleAsync(PricingIdentity identity, SchedulePriceCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is invalid.");
        var entry = command.ToEntry();
        var dataSource = source ?? throw new PricingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Prepare(connection, transaction, entry.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, entry.OrganizationId, entry.BranchId, identity, "pricing.manage", cancellationToken);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO pricing.prices(organization_id,price_id,operation_id,product_id,branch_id,amount,currency,
              tax_mode,tax_rate,valid_from,valid_until,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING price_id,product_id,branch_id,amount,currency,tax_mode,tax_rate,valid_from,valid_until,created_at
            """, connection, transaction);
        Add(insert, entry, command.OperationId, identity);
        PriceResponse? response;
        try
        {
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            response = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            throw new PricingConflictException();
        }
        if (response is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(response, true);
        }
        await using var replay = new NpgsqlCommand("""
            SELECT price_id,product_id,branch_id,amount,currency,tax_mode,tax_rate,valid_from,valid_until,created_at
            FROM pricing.prices WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        replay.Parameters.AddWithValue(entry.OrganizationId);
        replay.Parameters.AddWithValue(command.OperationId);
        await using var replayReader = await replay.ExecuteReaderAsync(cancellationToken);
        if (!await replayReader.ReadAsync(cancellationToken)) throw new PricingUnavailableException();
        response = Read(replayReader);
        if (response.ProductId != entry.ProductId || response.BranchId != entry.BranchId
            || response.Amount != entry.Price.Amount || response.Currency != entry.Price.Currency
            || response.TaxMode != Mode(entry.TaxMode) || response.TaxRate != entry.TaxRate
            || response.ValidFrom != entry.ValidFrom || response.ValidUntil != entry.ValidUntil)
            throw new PricingConflictException();
        await replayReader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(response, false);
    }

    public async Task<ResolvedPriceResponse?> ResolveAsync(PricingIdentity identity, Guid organizationId,
        Guid branchId, Guid productId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || productId == Guid.Empty
            || at == default || at.Offset != TimeSpan.Zero) throw new ArgumentException("Price query is invalid.");
        var dataSource = source ?? throw new PricingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, "pricing.view", cancellationToken);
        await using var query = new NpgsqlCommand("""
            SELECT price.price_id,price.product_id,price.branch_id,price.amount,price.currency,price.tax_mode,
              price.tax_rate,price.valid_from,price.valid_until
            FROM pricing.prices price JOIN catalog.products product
              ON product.organization_id=price.organization_id AND product.product_id=price.product_id AND product.is_active
            WHERE price.organization_id=$1 AND price.product_id=$2 AND (price.branch_id=$3 OR price.branch_id IS NULL)
              AND price.valid_from<=$4 AND (price.valid_until IS NULL OR price.valid_until>$4)
            ORDER BY price.branch_id IS NOT NULL DESC,price.valid_from DESC LIMIT 1
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(productId);
        query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(at);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        var result = await reader.ReadAsync(cancellationToken) ? new ResolvedPriceResponse(reader.GetGuid(0),
            reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4),
            reader.GetString(5), reader.GetDecimal(6), reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8)) : null;
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId,
        PricingIdentity identity, CancellationToken cancellationToken)
    {
        await using var safe = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='pricing' AND c.relname='prices' AND c.relrowsecurity AND c.relforcerowsecurity
                  AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safe.ExecuteScalarAsync(cancellationToken) is not true) throw new PricingUnavailableException();
        await using var context = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId,
        Guid? branchId, PricingIdentity identity, string permission, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g
              ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
            LEFT JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2
            JOIN organization.organizations organization ON organization.organization_id=m.organization_id
            LEFT JOIN organization.businesses business ON business.organization_id=b.organization_id
              AND business.business_id=b.business_id
            WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active
              AND organization.is_active AND ($2::uuid IS NULL OR business.is_active)
              AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
              AND g.permission=$5 AND (g.scope_kind='organization' OR ($2::uuid IS NOT NULL AND b.is_active AND
                ((g.scope_kind='business' AND g.business_id=b.business_id) OR
                 (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id) OR
                 (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))))
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)branchId ?? DBNull.Value });
        query.Parameters.AddWithValue(identity.Issuer); query.Parameters.AddWithValue(identity.Subject);
        query.Parameters.AddWithValue(permission);
        if (await query.ExecuteScalarAsync(cancellationToken) is not true) throw new PricingDeniedException();
    }

    private static void Add(NpgsqlCommand command, PriceEntry entry, Guid operationId, PricingIdentity identity)
    {
        command.Parameters.AddWithValue(entry.OrganizationId); command.Parameters.AddWithValue(entry.Id);
        command.Parameters.AddWithValue(operationId); command.Parameters.AddWithValue(entry.ProductId);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)entry.BranchId ?? DBNull.Value });
        command.Parameters.AddWithValue(entry.Price.Amount); command.Parameters.AddWithValue(entry.Price.Currency);
        command.Parameters.AddWithValue(Mode(entry.TaxMode)); command.Parameters.AddWithValue(entry.TaxRate);
        command.Parameters.AddWithValue(entry.ValidFrom);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = (object?)entry.ValidUntil ?? DBNull.Value });
        command.Parameters.AddWithValue(identity.Issuer); command.Parameters.AddWithValue(identity.Subject);
    }
    private static string Mode(TaxMode mode) => mode == TaxMode.Inclusive ? "inclusive" : "exclusive";
    private static PriceResponse Read(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1),
        reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4), reader.GetString(5),
        reader.GetDecimal(6), reader.GetFieldValue<DateTimeOffset>(7),
        reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8), reader.GetFieldValue<DateTimeOffset>(9));
}
