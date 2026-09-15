using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Purchasing.Application.PurchaseOrders;
using SalekhPos.Purchasing.Contracts.PurchaseOrders;
using SalekhPos.Purchasing.Domain.PurchaseOrders;

namespace SalekhPos.Purchasing.Infrastructure.PurchaseOrders;

public sealed class PostgresPurchaseOrderService(NpgsqlDataSource? source) : IPurchaseOrderService
{
    public async Task<PurchaseOrderWriteResult> CreateAsync(PurchasingIdentity identity,
        CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(command);
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
        var order = command.ToOrder();
        var dataSource = source ?? throw new PurchasingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Prepare(connection, transaction, order.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, order.OrganizationId, order.BranchId, identity,
            "purchase_orders.create", cancellationToken);
        await ValidateReferences(connection, transaction, order, cancellationToken);
        var createdAt = await DatabaseTime(connection, transaction, cancellationToken);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO purchasing.purchase_orders(organization_id,order_id,operation_id,branch_id,supplier_id,status,
              currency,reference,total,row_version,created_at,updated_at,issuer,subject)
            VALUES($1,$2,$3,$4,$5,'draft',$6,$7,$8,1,$9,$9,$10,$11)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING order_id,branch_id,supplier_id,status,currency,reference,total,row_version,created_at,updated_at
            """, connection, transaction);
        insert.Parameters.AddWithValue(order.OrganizationId);
        insert.Parameters.AddWithValue(order.Id);
        insert.Parameters.AddWithValue(command.OperationId);
        insert.Parameters.AddWithValue(order.BranchId);
        insert.Parameters.AddWithValue(order.SupplierId);
        insert.Parameters.AddWithValue(order.Currency);
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)order.Reference ?? DBNull.Value });
        insert.Parameters.AddWithValue(order.Total);
        insert.Parameters.AddWithValue(createdAt);
        insert.Parameters.AddWithValue(identity.Issuer);
        insert.Parameters.AddWithValue(identity.Subject);
        PurchaseOrderHeader? header;
        try
        {
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            header = await reader.ReadAsync(cancellationToken) ? ReadHeader(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new PurchaseOrderConflictException();
        }
        if (header is not null)
        {
            await InsertLines(connection, transaction, order, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ToResponse(header, order.Lines.Select(line => new PurchaseOrderLineResponse(line.ProductId, line.Quantity, line.UnitCost, line.LineTotal)).ToArray()), true);
        }

        var replay = await ReadByOperation(connection, transaction, order.OrganizationId,
            command.OperationId, cancellationToken) ?? throw new PurchasingUnavailableException();
        var replayLines = await LoadLines(connection, transaction, order.OrganizationId,
            [replay.Id], cancellationToken);
        if (!Equivalent(order, replay, replayLines.GetValueOrDefault(replay.Id, [])))
            throw new PurchaseOrderConflictException();
        await transaction.CommitAsync(cancellationToken);
        return new(ToResponse(replay, replayLines.GetValueOrDefault(replay.Id, [])), false);
    }

    public async Task<PurchaseOrderPage> ListAsync(PurchasingIdentity identity, Guid organizationId,
        Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        ValidateQuery(identity, organizationId, branchId, pageSize, after);
        var dataSource = source ?? throw new PurchasingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity,
            "purchase_orders.view", cancellationToken);
        await using var query = new NpgsqlCommand($"""
            {HeaderSelect}
            WHERE organization_id=$1 AND branch_id=$2 AND ($3::uuid IS NULL OR order_id>$3)
            ORDER BY order_id LIMIT $4
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(branchId);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = (object?)after ?? DBNull.Value });
        query.Parameters.AddWithValue(pageSize + 1);
        var headers = new List<PurchaseOrderHeader>(pageSize + 1);
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) headers.Add(ReadHeader(reader));
        Guid? next = null;
        if (headers.Count > pageSize)
        {
            headers.RemoveAt(pageSize);
            next = headers[^1].Id;
        }
        var lines = await LoadLines(connection, transaction, organizationId,
            headers.Select(item => item.Id).ToArray(), cancellationToken);
        var items = headers.Select(item => ToResponse(item, lines.GetValueOrDefault(item.Id, []))).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new(items, next);
    }

    public async Task<PurchaseOrderResponse?> ReadAsync(PurchasingIdentity identity, Guid organizationId,
        Guid branchId, Guid orderId, CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("Order ID is required.");
        ValidateQuery(identity, organizationId, branchId, 1, null);
        var dataSource = source ?? throw new PurchasingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity,
            "purchase_orders.view", cancellationToken);
        await using var query = new NpgsqlCommand($"""
            {HeaderSelect} WHERE organization_id=$1 AND branch_id=$2 AND order_id=$3
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(orderId);
        PurchaseOrderHeader? header;
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            header = await reader.ReadAsync(cancellationToken) ? ReadHeader(reader) : null;
        if (header is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var lines = await LoadLines(connection, transaction, organizationId, [orderId], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(header, lines.GetValueOrDefault(orderId, []));
    }

    public async Task<PurchaseOrderResponse> ChangeStatusAsync(PurchasingIdentity identity,
        ChangePurchaseOrderStatusCommand command, CancellationToken cancellationToken)
    {
        if (command.OrganizationId == Guid.Empty || command.BranchId == Guid.Empty || command.OrderId == Guid.Empty
            || command.ExpectedVersion < 1 || command.TargetStatus is not ("submitted" or "approved" or "cancelled"))
            throw new ArgumentException("Purchase order status change is invalid.");
        var dataSource = source ?? throw new PurchasingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Prepare(connection, transaction, command.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, command.OrganizationId, command.BranchId, identity,
            PermissionFor(command.TargetStatus), cancellationToken);
        var current = await ReadForUpdate(connection, transaction, command.OrganizationId, command.BranchId,
            command.OrderId, cancellationToken) ?? throw new PurchaseOrderNotFoundException();
        if (current.Version != command.ExpectedVersion || !CanTransition(current.Status, command.TargetStatus))
            throw new PurchaseOrderConflictException();
        var now = await DatabaseTime(connection, transaction, cancellationToken);
        await using var update = new NpgsqlCommand("""
            UPDATE purchasing.purchase_orders
            SET status=$4,row_version=row_version+1,updated_at=$5
            WHERE organization_id=$1 AND branch_id=$2 AND order_id=$3 AND row_version=$6
            RETURNING order_id,branch_id,supplier_id,status,currency,reference,total,row_version,created_at,updated_at
            """, connection, transaction);
        update.Parameters.AddWithValue(command.OrganizationId);
        update.Parameters.AddWithValue(command.BranchId);
        update.Parameters.AddWithValue(command.OrderId);
        update.Parameters.AddWithValue(command.TargetStatus);
        update.Parameters.AddWithValue(now);
        update.Parameters.AddWithValue(command.ExpectedVersion);
        PurchaseOrderHeader? changed;
        await using (var reader = await update.ExecuteReaderAsync(cancellationToken))
            changed = await reader.ReadAsync(cancellationToken) ? ReadHeader(reader) : null;
        if (changed is null) throw new PurchaseOrderConflictException();
        var lines = await LoadLines(connection, transaction, command.OrganizationId,
            [command.OrderId], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(changed, lines.GetValueOrDefault(command.OrderId, []));
    }

    private static async Task InsertLines(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PurchaseOrder order, CancellationToken cancellationToken)
    {
        var productIds = order.Lines.Select(line => line.ProductId).ToArray();
        var quantities = order.Lines.Select(line => line.Quantity).ToArray();
        var unitCosts = order.Lines.Select(line => line.UnitCost).ToArray();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO purchasing.purchase_order_lines(organization_id,order_id,line_number,product_id,quantity,unit_cost,line_total)
            SELECT $1,$2,ordinality::integer,product_id,quantity,unit_cost,quantity*unit_cost
            FROM unnest($3::uuid[],$4::numeric[],$5::numeric[]) WITH ORDINALITY
              AS x(product_id,quantity,unit_cost,ordinality)
            """, connection, transaction);
        insert.Parameters.AddWithValue(order.OrganizationId);
        insert.Parameters.AddWithValue(order.Id);
        insert.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Uuid, productIds);
        insert.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Numeric, quantities);
        insert.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Numeric, unitCosts);
        if (await insert.ExecuteNonQueryAsync(cancellationToken) != order.Lines.Count)
            throw new PurchasingUnavailableException();
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<PurchaseOrderLineResponse>>> LoadLines(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId,
        Guid[] orderIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<PurchaseOrderLineResponse>>();
        if (orderIds.Length == 0) return new();
        await using var query = new NpgsqlCommand("""
            SELECT order_id,product_id,quantity,unit_cost,line_total
            FROM purchasing.purchase_order_lines
            WHERE organization_id=$1 AND order_id=ANY($2::uuid[])
            ORDER BY order_id,line_number
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Uuid, orderIds);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var orderId = reader.GetGuid(0);
            if (!result.TryGetValue(orderId, out var items)) result[orderId] = items = [];
            items.Add(new(reader.GetGuid(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4)));
        }
        return result.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<PurchaseOrderLineResponse>)pair.Value.AsReadOnly());
    }

    private static async Task ValidateReferences(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PurchaseOrder order, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM organization.branches b JOIN organization.businesses z
              ON z.organization_id=b.organization_id AND z.business_id=b.business_id
              WHERE b.organization_id=$1 AND b.branch_id=$2 AND b.is_active AND z.is_active),
              EXISTS(SELECT FROM suppliers.suppliers WHERE organization_id=$1 AND supplier_id=$3 AND is_active),
              (SELECT count(*) FROM catalog.products WHERE organization_id=$1 AND product_id=ANY($4::uuid[]) AND is_active)
            """, connection, transaction);
        query.Parameters.AddWithValue(order.OrganizationId);
        query.Parameters.AddWithValue(order.BranchId);
        query.Parameters.AddWithValue(order.SupplierId);
        query.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Uuid,
            order.Lines.Select(line => line.ProductId).ToArray());
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || !reader.GetBoolean(0) || !reader.GetBoolean(1)
            || reader.GetInt64(2) != order.Lines.Count) throw new PurchaseOrderConflictException();
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, PurchasingIdentity identity, CancellationToken cancellationToken)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='purchasing' AND c.relname='purchase_orders' AND c.relrowsecurity
                  AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(cancellationToken) is not true) throw new PurchasingUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString());
        context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject);
        await context.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, PurchasingIdentity identity, string permission,
        CancellationToken cancellationToken)
    {
        await using var demand = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m
              JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2
              JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id
              JOIN organization.organizations o ON o.organization_id=b.organization_id
              WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active
                AND b.is_active AND z.is_active AND o.is_active AND m.valid_from<=statement_timestamp()
                AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$5
                AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
                  OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
                  OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
            """, connection, transaction);
        demand.Parameters.AddWithValue(organizationId); demand.Parameters.AddWithValue(branchId);
        demand.Parameters.AddWithValue(identity.Issuer); demand.Parameters.AddWithValue(identity.Subject);
        demand.Parameters.AddWithValue(permission);
        if (await demand.ExecuteScalarAsync(cancellationToken) is not true) throw new PurchasingDeniedException();
    }

    private static async Task<PurchaseOrderHeader?> ReadByOperation(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid operationId, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand($"""
            {HeaderSelect} WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(operationId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadHeader(reader) : null;
    }

    private static async Task<PurchaseOrderHeader?> ReadForUpdate(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid branchId, Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand($"""
            {HeaderSelect} WHERE organization_id=$1 AND branch_id=$2 AND order_id=$3 FOR UPDATE
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(orderId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadHeader(reader) : null;
    }

    private static bool Equivalent(PurchaseOrder order, PurchaseOrderHeader header,
        IReadOnlyList<PurchaseOrderLineResponse> lines)
    {
        if (header.Id != order.Id || header.BranchId != order.BranchId || header.SupplierId != order.SupplierId
            || header.Currency != order.Currency || header.Reference != order.Reference || header.Total != order.Total
            || lines.Count != order.Lines.Count) return false;
        for (var index = 0; index < lines.Count; index++)
        {
            var expected = order.Lines[index]; var actual = lines[index];
            if (expected.ProductId != actual.ProductId || expected.Quantity != actual.Quantity
                || expected.UnitCost != actual.UnitCost || expected.LineTotal != actual.LineTotal) return false;
        }
        return true;
    }

    private static bool CanTransition(string current, string target) =>
        (current, target) is ("draft", "submitted") or ("submitted", "approved")
            or ("draft", "cancelled") or ("submitted", "cancelled");

    private static string PermissionFor(string target) => target switch
    {
        "submitted" => "purchase_orders.submit",
        "approved" => "purchase_orders.approve",
        "cancelled" => "purchase_orders.cancel",
        _ => throw new ArgumentException("Purchase order target status is invalid.")
    };

    private static void ValidateQuery(PurchasingIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (organizationId == Guid.Empty || branchId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty)
            throw new ArgumentException("Purchase order query is invalid.");
    }

    private static async Task<DateTimeOffset> DatabaseTime(NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT statement_timestamp()", connection, transaction);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DateTimeOffset timestamp ? timestamp : throw new PurchasingUnavailableException();
    }

    private const string HeaderSelect = """
        SELECT order_id,branch_id,supplier_id,status,currency,reference,total,row_version,created_at,updated_at
        FROM purchasing.purchase_orders
        """;

    private static PurchaseOrderHeader ReadHeader(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1),
        reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetDecimal(6), reader.GetInt64(7), reader.GetFieldValue<DateTimeOffset>(8),
        reader.GetFieldValue<DateTimeOffset>(9));

    private static PurchaseOrderResponse ToResponse(PurchaseOrderHeader header,
        IReadOnlyList<PurchaseOrderLineResponse> lines) => new(header.Id, header.BranchId, header.SupplierId,
        header.Status, header.Currency, header.Reference, header.Total, header.Version, header.CreatedAt,
        header.UpdatedAt, lines);

    private sealed record PurchaseOrderHeader(Guid Id, Guid BranchId, Guid SupplierId, string Status,
        string Currency, string? Reference, decimal Total, long Version, DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
