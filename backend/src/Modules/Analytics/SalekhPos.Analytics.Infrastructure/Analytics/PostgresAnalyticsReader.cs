using Npgsql;
using NpgsqlTypes;
using SalekhPos.Analytics.Application.Analytics;
using SalekhPos.Analytics.Contracts.Analytics;
using SalekhPos.Analytics.Domain.Analytics;

namespace SalekhPos.Analytics.Infrastructure.Analytics;

public sealed class PostgresAnalyticsReader(NpgsqlDataSource? source) : IAnalyticsReader
{
    public async Task<AnalyticsOverviewResponse> ReadOverviewAsync(AnalyticsIdentity identity,
        Guid organizationId, Guid branchId, AnalyticsWindow window, CancellationToken ct)
    {
        Validate(identity, organizationId, branchId);
        var data = source ?? throw new AnalyticsUnavailableException();
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await DemandBranch(connection, transaction, organizationId, branchId, identity, ct);
        var currency = await ReadCurrency(connection, transaction, organizationId, branchId, window, ct);
        var core = await ReadCore(connection, transaction, organizationId, branchId, window, ct);
        var inventory = await ReadInventoryHealth(connection, transaction, organizationId, branchId, ct);
        await transaction.CommitAsync(ct);
        var average = core.Sales == 0 ? 0 : decimal.Round(core.Gross / core.Sales, 6, MidpointRounding.AwayFromZero);
        return new(branchId, window.From, window.To, currency, core.Sales, core.Gross,
            core.Returns, core.Refunds, core.Gross - core.Refunds, average, core.Products,
            inventory.Positive, inventory.Zero, inventory.Negative);
    }

    public async Task<SalesTrendResponse> ReadSalesTrendAsync(AnalyticsIdentity identity,
        Guid organizationId, Guid branchId, AnalyticsWindow window, CancellationToken ct)
    {
        Validate(identity, organizationId, branchId);
        var data = source ?? throw new AnalyticsUnavailableException();
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await DemandBranch(connection, transaction, organizationId, branchId, identity, ct);
        var currency = await ReadCurrency(connection, transaction, organizationId, branchId, window, ct);
        await using var query = new NpgsqlCommand("""
            WITH sale_days AS (
              SELECT date_trunc('day',completed_at) bucket_start,count(*)::int completed_sales,
                     COALESCE(sum(grand_total),0) gross_sales
              FROM sales.completed_sales
              WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
              GROUP BY 1
            ), return_days AS (
              SELECT date_trunc('day',completed_at) bucket_start,COALESCE(sum(amount),0) refunds
              FROM returns.completed_returns
              WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
              GROUP BY 1
            )
            SELECT COALESCE(s.bucket_start,r.bucket_start),COALESCE(s.completed_sales,0),
                   COALESCE(s.gross_sales,0),COALESCE(r.refunds,0)
            FROM sale_days s FULL OUTER JOIN return_days r USING(bucket_start)
            ORDER BY 1
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(window.From);
        query.Parameters.AddWithValue(window.To);
        var points = new List<SalesTrendPointResponse>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var bucket = reader.GetFieldValue<DateTimeOffset>(0);
                var sales = reader.GetInt32(1);
                var gross = reader.GetDecimal(2);
                var refunds = reader.GetDecimal(3);
                points.Add(new(bucket, currency, sales, gross, refunds, gross - refunds));
            }
        }
        await transaction.CommitAsync(ct);
        return new(branchId, window.From, window.To, points);
    }

    public async Task<StoreComparisonResponse> CompareStoresAsync(AnalyticsIdentity identity,
        Guid organizationId, AnalyticsWindow window, CancellationToken ct)
    {
        identity.Validate();
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        var data = source ?? throw new AnalyticsUnavailableException();
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Prepare(connection, transaction, organizationId, identity, ct);
        await DemandOrganization(connection, transaction, organizationId, identity, ct);
        await using var query = new NpgsqlCommand("""
            WITH activity AS (
              SELECT branch_id,currency,count(*)::int completed_sales,sum(grand_total)::numeric gross_sales,0::numeric refunds
              FROM sales.completed_sales
              WHERE organization_id=$1 AND completed_at >= $2 AND completed_at < $3
              GROUP BY branch_id,currency
              UNION ALL
              SELECT branch_id,currency,0::int,0::numeric,sum(amount)::numeric
              FROM returns.completed_returns
              WHERE organization_id=$1 AND completed_at >= $2 AND completed_at < $3
              GROUP BY branch_id,currency
            ), aggregate AS (
              SELECT branch_id,currency,sum(completed_sales)::int completed_sales,
                     COALESCE(sum(gross_sales),0) gross_sales,COALESCE(sum(refunds),0) refunds
              FROM activity GROUP BY branch_id,currency
            )
            SELECT b.branch_id,a.currency,COALESCE(a.completed_sales,0),
                   COALESCE(a.gross_sales,0),COALESCE(a.refunds,0)
            FROM organization.branches b
            LEFT JOIN aggregate a ON a.branch_id=b.branch_id
            WHERE b.organization_id=$1 AND b.is_active
            ORDER BY b.branch_id,a.currency
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(window.From);
        query.Parameters.AddWithValue(window.To);
        var rows = new List<StoreComparisonRowResponse>();
        var seen = new HashSet<Guid>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var branchId = reader.GetGuid(0);
                if (!seen.Add(branchId)) throw new AnalyticsUnavailableException();
                var currency = reader.IsDBNull(1) ? null : reader.GetString(1);
                var sales = reader.GetInt32(2); var gross = reader.GetDecimal(3); var refunds = reader.GetDecimal(4);
                var average = sales == 0 ? 0 : decimal.Round(gross / sales, 6, MidpointRounding.AwayFromZero);
                rows.Add(new(branchId, currency, sales, gross, refunds, gross - refunds, average));
            }
        }
        await transaction.CommitAsync(ct);
        return new(window.From, window.To, rows);
    }

    private sealed record Core(int Sales, decimal Gross, int Returns, decimal Refunds, int Products);
    private sealed record InventoryHealth(int Positive, int Zero, int Negative);

    private static async Task<Core> ReadCore(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, AnalyticsWindow window, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("""
            SELECT
              (SELECT count(*)::int FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),
              COALESCE((SELECT sum(grand_total) FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),0),
              (SELECT count(*)::int FROM returns.completed_returns
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),
              COALESCE((SELECT sum(amount) FROM returns.completed_returns
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),0),
              (SELECT count(DISTINCT l.product_id)::int FROM sales.sale_lines l
                JOIN sales.completed_sales s ON s.organization_id=l.organization_id AND s.sale_id=l.sale_id
                WHERE s.organization_id=$1 AND s.branch_id=$2 AND s.completed_at >= $3 AND s.completed_at < $4)
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(window.From); query.Parameters.AddWithValue(window.To);
        await using var reader = await query.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new AnalyticsUnavailableException();
        return new(reader.GetInt32(0), reader.GetDecimal(1), reader.GetInt32(2), reader.GetDecimal(3), reader.GetInt32(4));
    }
    private static async Task<InventoryHealth> ReadInventoryHealth(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid branchId, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("""
            WITH balances AS (
              SELECT p.product_id,COALESCE(sum(m.direction*m.quantity),0) quantity
              FROM catalog.products p
              LEFT JOIN inventory.stock_movements m ON m.organization_id=p.organization_id
                AND m.product_id=p.product_id AND m.branch_id=$2
              WHERE p.organization_id=$1 AND p.is_active
              GROUP BY p.product_id
            )
            SELECT count(*) FILTER(WHERE quantity>0)::int,
                   count(*) FILTER(WHERE quantity=0)::int,
                   count(*) FILTER(WHERE quantity<0)::int
            FROM balances
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        await using var reader = await query.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new AnalyticsUnavailableException();
        return new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    private static async Task<string?> ReadCurrency(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid branchId,
        AnalyticsWindow window, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("""
            SELECT DISTINCT currency FROM (
              SELECT currency FROM sales.completed_sales
              WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
              UNION SELECT currency FROM returns.completed_returns
              WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
            ) x LIMIT 2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(window.From); query.Parameters.AddWithValue(window.To);
        var currencies = new List<string>(2);
        await using var reader = await query.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) currencies.Add(reader.GetString(0));
        if (currencies.Count > 1) throw new AnalyticsUnavailableException();
        return currencies.Count == 0 ? null : currencies[0];
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, AnalyticsIdentity identity, CancellationToken ct)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime' AND EXISTS(
              SELECT FROM pg_class x JOIN pg_namespace n ON n.oid=x.relnamespace
              WHERE n.nspname='sales' AND x.relname='completed_sales'
                AND x.relrowsecurity AND x.relforcerowsecurity
                AND x.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(ct) is not true) throw new AnalyticsUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString());
        context.Parameters.AddWithValue(identity.Issuer); context.Parameters.AddWithValue(identity.Subject);
        await context.ExecuteNonQueryAsync(ct);
    }

    private static async Task DemandBranch(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, AnalyticsIdentity identity, CancellationToken ct)
    {
        await using var demand = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m
              JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2
              JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id
              JOIN organization.organizations o ON o.organization_id=b.organization_id
              WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active
                AND b.is_active AND z.is_active AND o.is_active
                AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission='reports.view'
                AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
                  OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
                  OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
            """, connection, transaction);
        demand.Parameters.AddWithValue(organizationId); demand.Parameters.AddWithValue(branchId);
        demand.Parameters.AddWithValue(identity.Issuer); demand.Parameters.AddWithValue(identity.Subject);
        if (await demand.ExecuteScalarAsync(ct) is not true) throw new AnalyticsDeniedException();
    }

    private static async Task DemandOrganization(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, AnalyticsIdentity identity, CancellationToken ct)
    {
        await using var demand = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m
              JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active
                AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission='reports.view' AND g.scope_kind='organization')
            """, connection, transaction);
        demand.Parameters.AddWithValue(organizationId);
        demand.Parameters.AddWithValue(identity.Issuer); demand.Parameters.AddWithValue(identity.Subject);
        if (await demand.ExecuteScalarAsync(ct) is not true) throw new AnalyticsDeniedException();
    }

    private static void Validate(AnalyticsIdentity identity, Guid organizationId, Guid branchId)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Analytics scope is invalid.");
    }
}
