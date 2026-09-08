using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Inventory.Application.Stock;
using SalekhPos.Inventory.Contracts.Stock;
using SalekhPos.Inventory.Domain.StockMovements;

namespace SalekhPos.Inventory.Infrastructure.Stock;

public sealed class PostgresInventoryLedger(NpgsqlDataSource? source) : IInventoryLedger
{
    public async Task<StockMovementWriteResult> RecordAsync(InventoryIdentity identity,
        CreateStockMovementCommand command, CancellationToken cancellationToken)
    {
        var movement = command.ToMovement();
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is invalid.");
        var dataSource = source ?? throw new InventoryUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Safe(connection, transaction, cancellationToken);
        await Context(connection, transaction, movement.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, movement.OrganizationId, movement.BranchId, identity,
            "inventory.adjust", cancellationToken);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movements(organization_id,movement_id,operation_id,branch_id,product_id,
              kind,direction,quantity,reason,occurred_at,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING movement_id,branch_id,product_id,kind,quantity,reason,occurred_at,recorded_at
            """, connection, transaction);
        AddMovement(insert, movement, command.OperationId, identity);
        StockMovementResponse? response;
        await using (var reader = await insert.ExecuteReaderAsync(cancellationToken))
            response = await reader.ReadAsync(cancellationToken) ? ReadMovement(reader) : null;
        if (response is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(response, true);
        }
        await using var replay = new NpgsqlCommand("""
            SELECT movement_id,branch_id,product_id,kind,quantity,reason,occurred_at,recorded_at
            FROM inventory.stock_movements WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        replay.Parameters.AddWithValue(movement.OrganizationId); replay.Parameters.AddWithValue(command.OperationId);
        await using var replayReader = await replay.ExecuteReaderAsync(cancellationToken);
        if (!await replayReader.ReadAsync(cancellationToken)) throw new InventoryUnavailableException();
        response = ReadMovement(replayReader);
        if (response.BranchId != movement.BranchId || response.ProductId != movement.ProductId
            || response.Kind != Kind(movement.Kind) || response.Quantity != movement.Quantity
            || response.Reason != movement.Reason || response.OccurredAt != movement.OccurredAt)
            throw new InventoryConflictException();
        await replayReader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(response, false);
    }

    public async Task<StockPage> ReadStockAsync(InventoryIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty)
            throw new ArgumentException("Stock query is invalid.");
        var dataSource = source ?? throw new InventoryUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Safe(connection, transaction, cancellationToken);
        await Context(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, "inventory.view", cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT p.product_id,p.sku,p.name,coalesce(sum(m.direction*m.quantity),0)::numeric(20,6)
            FROM catalog.products p LEFT JOIN inventory.stock_movements m
              ON m.organization_id=p.organization_id AND m.product_id=p.product_id AND m.branch_id=$2
            WHERE p.organization_id=$1 AND p.is_active AND ($3::uuid IS NULL OR p.product_id>$3)
            GROUP BY p.product_id,p.sku,p.name ORDER BY p.product_id LIMIT $4
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(branchId);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)after ?? DBNull.Value });
        command.Parameters.AddWithValue(pageSize + 1);
        var items = new List<StockLevelResponse>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) items.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3)));
        await transaction.CommitAsync(cancellationToken);
        var next = items.Count > pageSize ? items[pageSize - 1].ProductId : (Guid?)null;
        if (items.Count > pageSize) items.RemoveAt(pageSize);
        return new(items.AsReadOnly(), next);
    }

    private static async Task Context(NpgsqlConnection c, NpgsqlTransaction t, Guid org, InventoryIdentity id, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t);
        q.Parameters.AddWithValue(org.ToString()); q.Parameters.AddWithValue(id.Issuer); q.Parameters.AddWithValue(id.Subject); await q.ExecuteNonQueryAsync(ct);
    }
    private static async Task Safe(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("""
          SELECT current_user='salekhpos_runtime' AND NOT EXISTS(SELECT FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
          AND NOT EXISTS(SELECT FROM pg_auth_members WHERE member=(SELECT oid FROM pg_roles WHERE rolname=current_user))
          AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='inventory' AND c.relname='stock_movements' AND c.relrowsecurity AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
          """, c, t);
        if (await q.ExecuteScalarAsync(ct) is not true) throw new InventoryUnavailableException();
    }
    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid branch, InventoryIdentity id, string permission, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("""
          SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
          JOIN organization.branches b ON b.organization_id=m.organization_id
          JOIN organization.businesses business ON business.organization_id=b.organization_id AND business.business_id=b.business_id
          JOIN organization.organizations organization ON organization.organization_id=b.organization_id
          WHERE m.organization_id=$1 AND b.branch_id=$2 AND b.is_active AND business.is_active AND organization.is_active
          AND m.issuer=$3 AND m.subject=$4 AND m.is_active
          AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$5
          AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
          OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
          OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
          """, c, t);
        q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(branch); q.Parameters.AddWithValue(id.Issuer); q.Parameters.AddWithValue(id.Subject); q.Parameters.AddWithValue(permission);
        if (await q.ExecuteScalarAsync(ct) is not true) throw new InventoryDeniedException();
    }
    private static void AddMovement(NpgsqlCommand q, StockMovement m, Guid op, InventoryIdentity id)
    {
        q.Parameters.AddWithValue(m.OrganizationId); q.Parameters.AddWithValue(m.Id); q.Parameters.AddWithValue(op); q.Parameters.AddWithValue(m.BranchId); q.Parameters.AddWithValue(m.ProductId);
        q.Parameters.AddWithValue(Kind(m.Kind)); q.Parameters.AddWithValue((short)m.Direction); q.Parameters.AddWithValue(m.Quantity);
        q.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)m.Reason ?? DBNull.Value }); q.Parameters.AddWithValue(m.OccurredAt); q.Parameters.AddWithValue(id.Issuer); q.Parameters.AddWithValue(id.Subject);
    }
    private static string Kind(StockMovementKind k) => k switch { StockMovementKind.Receipt => "receipt", StockMovementKind.AdjustmentIn => "adjustment_in", StockMovementKind.AdjustmentOut => "adjustment_out", StockMovementKind.Sale => "sale", StockMovementKind.Return => "return", _ => throw new ArgumentOutOfRangeException(nameof(k)) };
    private static StockMovementResponse ReadMovement(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetDecimal(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetFieldValue<DateTimeOffset>(6), r.GetFieldValue<DateTimeOffset>(7));
}
