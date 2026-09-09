using Npgsql;
using SalekhPos.Payments.Application.Payments;
using SalekhPos.Payments.Contracts.Payments;

namespace SalekhPos.Payments.Infrastructure.Payments;

public sealed class PostgresPaymentReader(NpgsqlDataSource? source) : IPaymentReader
{
    public async Task<PaymentResponse?> ReadForSaleAsync(PaymentIdentity identity, Guid organizationId,
        Guid branchId, Guid saleId, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || saleId == Guid.Empty)
            throw new ArgumentException("Payment query is invalid.");
        var dataSource = source ?? throw new PaymentsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var safety = new NpgsqlCommand("SELECT current_user='salekhpos_runtime' AND c.relrowsecurity AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='payments' AND c.relname='payment_records'", connection, transaction))
            if (await safety.ExecuteScalarAsync(cancellationToken) is not true) throw new PaymentsUnavailableException();
        await using (var context = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", connection, transaction))
        {
            context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer);
            context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var demand = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g
              ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
            JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2
            JOIN organization.businesses business ON business.organization_id=b.organization_id AND business.business_id=b.business_id
            JOIN organization.organizations organization ON organization.organization_id=b.organization_id
            WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active AND b.is_active
              AND business.is_active AND organization.is_active AND m.valid_from<=statement_timestamp()
              AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission='payments.view'
              AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
                OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
                OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
            """, connection, transaction))
        {
            demand.Parameters.AddWithValue(organizationId); demand.Parameters.AddWithValue(branchId);
            demand.Parameters.AddWithValue(identity.Issuer); demand.Parameters.AddWithValue(identity.Subject);
            if (await demand.ExecuteScalarAsync(cancellationToken) is not true) throw new PaymentDeniedException();
        }
        await using var query = new NpgsqlCommand("SELECT payment_id,sale_id,branch_id,method,status,currency,amount,tendered,change_amount,completed_at FROM payments.payment_records WHERE organization_id=$1 AND branch_id=$2 AND sale_id=$3", connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(saleId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        PaymentResponse? result = await reader.ReadAsync(cancellationToken) ? new(reader.GetGuid(0), reader.GetGuid(1),
            reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetDecimal(6),
            reader.GetDecimal(7), reader.GetDecimal(8), reader.GetFieldValue<DateTimeOffset>(9)) : null;
        await reader.DisposeAsync(); await transaction.CommitAsync(cancellationToken); return result;
    }

    public Task<RefundResponse?> ReadForReturnAsync(PaymentIdentity identity, Guid organizationId, Guid branchId,
        Guid returnId, CancellationToken cancellationToken) => ReadRefundAsync(identity, organizationId, branchId,
            returnId, "return", cancellationToken);

    public Task<RefundResponse?> ReadForVoidAsync(PaymentIdentity identity, Guid organizationId, Guid branchId,
        Guid voidId, CancellationToken cancellationToken) => ReadRefundAsync(identity, organizationId, branchId,
            voidId, "void", cancellationToken);

    private async Task<RefundResponse?> ReadRefundAsync(PaymentIdentity identity, Guid organizationId,
        Guid branchId, Guid sourceId, string sourceKind, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || sourceId == Guid.Empty)
            throw new ArgumentException("Refund query is invalid.");
        var dataSource = source ?? throw new PaymentsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetContextAndDemand(connection, transaction, identity, organizationId, branchId, cancellationToken);
        var sql = sourceKind == "return"
            ? "SELECT r.refund_id,r.payment_id,r.branch_id,'return',r.return_id,r.method,r.status,r.currency,r.amount,r.completed_at FROM payments.refund_records r WHERE r.organization_id=$1 AND r.branch_id=$2 AND r.return_id=$3"
            : "SELECT v.void_id,v.payment_id,s.branch_id,'void',v.void_id,'cash','completed',v.currency,v.amount,v.completed_at FROM payments.void_refunds v JOIN sales.sale_voids s ON s.organization_id=v.organization_id AND s.void_id=v.void_id WHERE v.organization_id=$1 AND s.branch_id=$2 AND v.void_id=$3";
        await using var query = new NpgsqlCommand(sql, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(sourceId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        RefundResponse? result = await reader.ReadAsync(cancellationToken) ? new(reader.GetGuid(0), reader.GetGuid(1),
            reader.GetGuid(2), reader.GetString(3), reader.GetGuid(4), reader.GetString(5), reader.GetString(6),
            reader.GetString(7), reader.GetDecimal(8), reader.GetFieldValue<DateTimeOffset>(9)) : null;
        await reader.DisposeAsync(); await transaction.CommitAsync(cancellationToken); return result;
    }

    private static async Task SetContextAndDemand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PaymentIdentity identity, Guid organizationId, Guid branchId, CancellationToken cancellationToken)
    {
        await using (var context = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", connection, transaction))
        { context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer); context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(cancellationToken); }
        await using var demand = new NpgsqlCommand("SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2 JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id JOIN organization.organizations o ON o.organization_id=b.organization_id WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active AND b.is_active AND z.is_active AND o.is_active AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission='payments.view' AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id) OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id) OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))", connection, transaction);
        demand.Parameters.AddWithValue(organizationId); demand.Parameters.AddWithValue(branchId); demand.Parameters.AddWithValue(identity.Issuer); demand.Parameters.AddWithValue(identity.Subject);
        if (await demand.ExecuteScalarAsync(cancellationToken) is not true) throw new PaymentDeniedException();
    }
}
