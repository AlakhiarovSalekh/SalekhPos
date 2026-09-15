using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Warehousing.Application.Transfers;
using SalekhPos.Warehousing.Contracts.Transfers;
using SalekhPos.Warehousing.Domain.Dispatch;

namespace SalekhPos.Warehousing.Infrastructure.Transfers;

public sealed class PostgresStockTransferService(NpgsqlDataSource? source) : IStockTransferService
{
    private sealed record Header(Guid OrganizationId, Guid Id, Guid SourceBranchId, Guid DestinationBranchId, string Status,
        string? Reference, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
        DateTimeOffset? DispatchedAt, DateTimeOffset? ReceivedAt);
    private sealed record Line(Guid ProductId, decimal Quantity, Guid DispatchOperationId, Guid ReceiveOperationId);

    public async Task<StockTransferWriteResult> CreateAsync(WarehousingIdentity identity,
        CreateStockTransferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(command);
        identity.Validate();
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
        var transfer = command.ToTransfer();
        var dataSource = source ?? throw new WarehousingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Prepare(connection, transaction, transfer.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, transfer.OrganizationId, transfer.SourceBranchId, identity,
            "inventory.adjust", cancellationToken);
        await Demand(connection, transaction, transfer.OrganizationId, transfer.DestinationBranchId, identity,
            "inventory.adjust", cancellationToken);
        await ValidateReferences(connection, transaction, transfer, cancellationToken);
        var now = await DatabaseTime(connection, transaction, cancellationToken);
        var inserted = await InsertHeader(connection, transaction, transfer, command.OperationId,
            identity, now, cancellationToken);
        if (inserted is not null)
        {
            var lines = await InsertLines(connection, transaction, transfer, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ToResponse(inserted, lines), true);
        }

        var replay = await ReadByOperation(connection, transaction, transfer.OrganizationId,
            command.OperationId, cancellationToken) ?? throw new WarehousingUnavailableException();
        var replayLines = await LoadLines(connection, transaction, transfer.OrganizationId,
            replay.Id, cancellationToken);
        if (!Equivalent(transfer, replay, replayLines)) throw new StockTransferConflictException();
        await transaction.CommitAsync(cancellationToken);
        return new(ToResponse(replay, replayLines), false);
    }

    public async Task<StockTransferPage> ListAsync(WarehousingIdentity identity, Guid organizationId,
        Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        ValidateQuery(identity, organizationId, branchId, pageSize, after);
        var dataSource = source ?? throw new WarehousingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, "inventory.view", cancellationToken);
        await using var query = new NpgsqlCommand($"""
            {HeaderSelect}
            WHERE organization_id=$1 AND (source_branch_id=$2 OR destination_branch_id=$2)
              AND ($3::uuid IS NULL OR transfer_id>$3)
            ORDER BY transfer_id LIMIT $4
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = after.HasValue ? after.Value : DBNull.Value });
        query.Parameters.AddWithValue(pageSize + 1);
        var headers = new List<Header>();
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) headers.Add(ReadHeader(reader));
        Guid? next = null;
        if (headers.Count > pageSize) { headers.RemoveAt(pageSize); next = headers[^1].Id; }
        var items = new List<StockTransferResponse>(headers.Count);
        foreach (var header in headers)
            items.Add(ToResponse(header, await LoadLines(connection, transaction, organizationId, header.Id, cancellationToken)));
        await transaction.CommitAsync(cancellationToken);
        return new(items.AsReadOnly(), next);
    }

    public async Task<StockTransferResponse?> ReadAsync(WarehousingIdentity identity, Guid organizationId,
        Guid branchId, Guid transferId, CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || branchId == Guid.Empty || transferId == Guid.Empty)
            throw new ArgumentException("Stock transfer query is invalid.");
        var dataSource = source ?? throw new WarehousingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, "inventory.view", cancellationToken);
        await using var query = new NpgsqlCommand($"""
            {HeaderSelect}
            WHERE organization_id=$1 AND transfer_id=$2 AND (source_branch_id=$3 OR destination_branch_id=$3)
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(transferId);
        query.Parameters.AddWithValue(branchId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) { await transaction.CommitAsync(cancellationToken); return null; }
        var header = ReadHeader(reader); await reader.DisposeAsync();
        var lines = await LoadLines(connection, transaction, organizationId, header.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(header, lines);
    }

    public Task<StockTransferResponse> DispatchAsync(WarehousingIdentity identity, Guid organizationId,
        Guid sourceBranchId, Guid transferId, long expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(identity, organizationId, sourceBranchId, transferId, expectedVersion,
            "dispatch", cancellationToken);

    public Task<StockTransferResponse> ReceiveAsync(WarehousingIdentity identity, Guid organizationId,
        Guid destinationBranchId, Guid transferId, long expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(identity, organizationId, destinationBranchId, transferId, expectedVersion,
            "receive", cancellationToken);

    public Task<StockTransferResponse> CancelAsync(WarehousingIdentity identity, Guid organizationId,
        Guid branchId, Guid transferId, long expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(identity, organizationId, branchId, transferId, expectedVersion,
            "cancel", cancellationToken);
    private async Task<StockTransferResponse> TransitionAsync(WarehousingIdentity identity, Guid organizationId,
        Guid branchId, Guid transferId, long expectedVersion, string action, CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty || branchId == Guid.Empty || transferId == Guid.Empty || expectedVersion < 1)
            throw new ArgumentException("Stock transfer transition is invalid.");
        var dataSource = source ?? throw new WarehousingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Lock(connection, transaction, $"stock-transfer:{organizationId:D}:{transferId:D}", cancellationToken);
        var header = await ReadForUpdate(connection, transaction, organizationId, transferId, cancellationToken)
            ?? throw new StockTransferNotFoundException();
        if (action == "dispatch" && header.SourceBranchId != branchId
            || action == "receive" && header.DestinationBranchId != branchId
            || action == "cancel" && header.SourceBranchId != branchId && header.DestinationBranchId != branchId)
            throw new StockTransferNotFoundException();
        await Demand(connection, transaction, organizationId, branchId, identity,
            action == "cancel" ? "inventory.adjust" : "inventory.adjust", cancellationToken);
        if (header.Version != expectedVersion) throw new StockTransferConflictException();
        var lines = await LoadLines(connection, transaction, organizationId, transferId, cancellationToken);
        if (lines.Count == 0) throw new WarehousingUnavailableException();
        var now = await DatabaseTime(connection, transaction, cancellationToken);
        if (action == "dispatch") await Dispatch(connection, transaction, header, lines, identity, now, cancellationToken);
        else if (action == "receive") await Receive(connection, transaction, header, lines, identity, now, cancellationToken);
        else if (action == "cancel") await Cancel(connection, transaction, header, now, cancellationToken);
        else throw new ArgumentException("Stock transfer action is invalid.");
        var changed = await ReadForUpdate(connection, transaction, organizationId, transferId, cancellationToken)
            ?? throw new WarehousingUnavailableException();
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(changed, lines);
    }

    private static async Task Dispatch(NpgsqlConnection connection, NpgsqlTransaction transaction, Header header,
        IReadOnlyList<Line> lines, WarehousingIdentity identity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (header.Status != "draft") throw new StockTransferConflictException();
        foreach (var line in lines.OrderBy(line => line.ProductId))
        {
            await Lock(connection, transaction,
                $"stock:{header.OrganizationId:D}:{header.SourceBranchId:D}:{line.ProductId:D}",
                cancellationToken);
        }
        foreach (var line in lines)
        {
            var available = await Stock(connection, transaction, header.OrganizationId, header.SourceBranchId, line.ProductId, cancellationToken);
            if (available < line.Quantity) throw new StockTransferInsufficientStockException();
            await InsertMovement(connection, transaction, header, line, header.SourceBranchId,
                "transfer_out", -1, line.DispatchOperationId, identity, now, cancellationToken);
        }
        await UpdateStatus(connection, transaction, header, "in_transit", now,
            dispatchedAt: now, receivedAt: null, cancellationToken);
    }

    private static async Task Receive(NpgsqlConnection connection, NpgsqlTransaction transaction, Header header,
        IReadOnlyList<Line> lines, WarehousingIdentity identity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (header.Status != "in_transit") throw new StockTransferConflictException();
        foreach (var line in lines.OrderBy(line => line.ProductId))
            await Lock(connection, transaction,
                $"stock:{header.OrganizationId:D}:{header.DestinationBranchId:D}:{line.ProductId:D}", cancellationToken);
        foreach (var line in lines)
            await InsertMovement(connection, transaction, header, line, header.DestinationBranchId,
                "transfer_in", 1, line.ReceiveOperationId, identity, now, cancellationToken);
        await UpdateStatus(connection, transaction, header, "received", now,
            dispatchedAt: header.DispatchedAt, receivedAt: now, cancellationToken);
    }

    private static async Task Cancel(NpgsqlConnection connection, NpgsqlTransaction transaction, Header header,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (header.Status != "draft") throw new StockTransferConflictException();
        await UpdateStatus(connection, transaction, header, "cancelled", now,
            dispatchedAt: null, receivedAt: null, cancellationToken);
    }

    private static async Task InsertMovement(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Header header, Line line, Guid branchId, string kind, short direction, Guid operationId,
        WarehousingIdentity identity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movements(organization_id,movement_id,operation_id,branch_id,product_id,
              kind,direction,quantity,reason,occurred_at,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            """, connection, transaction);
        command.Parameters.AddWithValue(header.OrganizationId); command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(operationId); command.Parameters.AddWithValue(branchId);
        command.Parameters.AddWithValue(line.ProductId); command.Parameters.AddWithValue(kind);
        command.Parameters.AddWithValue(direction); command.Parameters.AddWithValue(line.Quantity);
        command.Parameters.AddWithValue($"Stock transfer {header.Id:D}"); command.Parameters.AddWithValue(now);
        command.Parameters.AddWithValue(identity.Issuer); command.Parameters.AddWithValue(identity.Subject);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateStatus(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Header header, string status, DateTimeOffset updatedAt, DateTimeOffset? dispatchedAt,
        DateTimeOffset? receivedAt, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE warehousing.stock_transfers SET status=$2,row_version=row_version+1,updated_at=$3,
              dispatched_at=$4,received_at=$5 WHERE organization_id=$1 AND transfer_id=$6
            """, connection, transaction);
        command.Parameters.AddWithValue(header.OrganizationId); command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(updatedAt);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz,
            Value = dispatchedAt.HasValue ? dispatchedAt.Value : DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz,
            Value = receivedAt.HasValue ? receivedAt.Value : DBNull.Value });
        command.Parameters.AddWithValue(header.Id);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new WarehousingUnavailableException();
    }

    private static async Task<decimal> Stock(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, Guid productId, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("SELECT coalesce(sum(direction*quantity),0)::numeric(20,6) FROM inventory.stock_movements WHERE organization_id=$1 AND branch_id=$2 AND product_id=$3", connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(productId);
        return (decimal)(await query.ExecuteScalarAsync(cancellationToken) ?? 0m);
    }

    private static async Task<Header?> InsertHeader(NpgsqlConnection connection, NpgsqlTransaction transaction,
        StockTransfer transfer, Guid operationId, WarehousingIdentity identity, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO warehousing.stock_transfers(organization_id,transfer_id,operation_id,source_branch_id,
              destination_branch_id,status,reference,created_at,updated_at,issuer,subject)
            VALUES($1,$2,$3,$4,$5,'draft',$6,$7,$7,$8,$9)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING organization_id,transfer_id,source_branch_id,destination_branch_id,status,reference,row_version,
              created_at,updated_at,dispatched_at,received_at
            """, connection, transaction);
        command.Parameters.AddWithValue(transfer.OrganizationId); command.Parameters.AddWithValue(transfer.Id);
        command.Parameters.AddWithValue(operationId); command.Parameters.AddWithValue(transfer.SourceBranchId);
        command.Parameters.AddWithValue(transfer.DestinationBranchId);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)transfer.Reference ?? DBNull.Value });
        command.Parameters.AddWithValue(now); command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadHeader(reader) : null;
    }

    private static async Task<IReadOnlyList<Line>> InsertLines(NpgsqlConnection connection,
        NpgsqlTransaction transaction, StockTransfer transfer, CancellationToken cancellationToken)
    {        var rows = new List<Line>(transfer.Lines.Count);
        for (var index = 0; index < transfer.Lines.Count; index++)
        {
            var line = transfer.Lines[index];
            var row = new Line(line.ProductId, line.Quantity, Guid.NewGuid(), Guid.NewGuid());
            await using var command = new NpgsqlCommand("""
                INSERT INTO warehousing.stock_transfer_lines(organization_id,transfer_id,line_no,product_id,quantity,
                  dispatch_operation_id,receive_operation_id) VALUES($1,$2,$3,$4,$5,$6,$7)
                """, connection, transaction);
            command.Parameters.AddWithValue(transfer.OrganizationId); command.Parameters.AddWithValue(transfer.Id);
            command.Parameters.AddWithValue(index + 1); command.Parameters.AddWithValue(row.ProductId);
            command.Parameters.AddWithValue(row.Quantity); command.Parameters.AddWithValue(row.DispatchOperationId);
            command.Parameters.AddWithValue(row.ReceiveOperationId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            rows.Add(row);
        }
        return rows.AsReadOnly();
    }

    private static async Task<IReadOnlyList<Line>> LoadLines(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid transferId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT product_id,quantity,dispatch_operation_id,receive_operation_id
            FROM warehousing.stock_transfer_lines
            WHERE organization_id=$1 AND transfer_id=$2 ORDER BY line_no
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(transferId);
        var rows = new List<Line>();        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(reader.GetGuid(0), reader.GetDecimal(1), reader.GetGuid(2), reader.GetGuid(3)));
        return rows.AsReadOnly();
    }

    private static async Task<Header?> ReadByOperation(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid operationId, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand($"""
            {HeaderSelect} WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(operationId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadHeader(reader) : null;
    }

    private static async Task<Header?> ReadForUpdate(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid transferId, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand($"""
            {HeaderSelect} WHERE organization_id=$1 AND transfer_id=$2 FOR UPDATE
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(transferId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadHeader(reader) : null;
    }
    private static async Task ValidateReferences(NpgsqlConnection connection, NpgsqlTransaction transaction,
        StockTransfer transfer, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT
              EXISTS(SELECT FROM organization.branches b JOIN organization.businesses z
                ON z.organization_id=b.organization_id AND z.business_id=b.business_id
                WHERE b.organization_id=$1 AND b.branch_id=$2 AND b.is_active AND z.is_active),
              EXISTS(SELECT FROM organization.branches b JOIN organization.businesses z
                ON z.organization_id=b.organization_id AND z.business_id=b.business_id
                WHERE b.organization_id=$1 AND b.branch_id=$3 AND b.is_active AND z.is_active),
              (SELECT count(*) FROM catalog.products
                WHERE organization_id=$1 AND product_id=ANY($4::uuid[]) AND is_active)
            """, connection, transaction);
        query.Parameters.AddWithValue(transfer.OrganizationId); query.Parameters.AddWithValue(transfer.SourceBranchId);
        query.Parameters.AddWithValue(transfer.DestinationBranchId);
        query.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Uuid,
            transfer.Lines.Select(line => line.ProductId).ToArray());
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || !reader.GetBoolean(0) || !reader.GetBoolean(1)
            || reader.GetInt64(2) != transfer.Lines.Count) throw new StockTransferConflictException();
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, WarehousingIdentity identity, CancellationToken cancellationToken)
    {        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='warehousing' AND c.relname='stock_transfers' AND c.relrowsecurity
                  AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(cancellationToken) is not true) throw new WarehousingUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, WarehousingIdentity identity, string permission,
        CancellationToken cancellationToken)
    {
        await using var demand = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m
              JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2
              JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id
              JOIN organization.organizations o ON o.organization_id=b.organization_id
              WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active
                AND b.is_active AND z.is_active AND o.is_active AND m.valid_from<=statement_timestamp()                AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$5
                AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
                  OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
                  OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
            """, connection, transaction);
        demand.Parameters.AddWithValue(organizationId); demand.Parameters.AddWithValue(branchId);
        demand.Parameters.AddWithValue(identity.Issuer); demand.Parameters.AddWithValue(identity.Subject);
        demand.Parameters.AddWithValue(permission);
        if (await demand.ExecuteScalarAsync(cancellationToken) is not true) throw new WarehousingDeniedException();
    }

    private static async Task Lock(NpgsqlConnection connection, NpgsqlTransaction transaction, string key,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction);
        query.Parameters.AddWithValue(key); await query.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<DateTimeOffset> DatabaseTime(NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT statement_timestamp()", connection, transaction);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DateTimeOffset timestamp ? timestamp : throw new WarehousingUnavailableException();
    }
    private static bool Equivalent(StockTransfer transfer, Header header, IReadOnlyList<Line> lines)
    {
        if (header.Id != transfer.Id || header.SourceBranchId != transfer.SourceBranchId
            || header.DestinationBranchId != transfer.DestinationBranchId || header.Reference != transfer.Reference
            || lines.Count != transfer.Lines.Count) return false;
        for (var index = 0; index < lines.Count; index++)
        {
            var expected = transfer.Lines[index]; var actual = lines[index];
            if (expected.ProductId != actual.ProductId || expected.Quantity != actual.Quantity) return false;
        }
        return true;
    }

    private static StockTransferResponse ToResponse(Header header, IReadOnlyList<Line> lines) => new(
        header.Id, header.SourceBranchId, header.DestinationBranchId, header.Status, header.Reference,
        header.Version, header.CreatedAt, header.UpdatedAt, header.DispatchedAt, header.ReceivedAt,
        lines.Select(line => new StockTransferLineResponse(line.ProductId, line.Quantity)).ToArray());

    private static Header ReadHeader(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt64(6),
        reader.GetFieldValue<DateTimeOffset>(7), reader.GetFieldValue<DateTimeOffset>(8),
        reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
        reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10));

    private const string HeaderSelect = """
        SELECT organization_id,transfer_id,source_branch_id,destination_branch_id,status,reference,row_version,
          created_at,updated_at,dispatched_at,received_at
        FROM warehousing.stock_transfers
        """;

    private static void ValidateQuery(WarehousingIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after)
    {
        ArgumentNullException.ThrowIfNull(identity); identity.Validate();
        if (organizationId == Guid.Empty || branchId == Guid.Empty || pageSize is < 1 or > 100
            || after == Guid.Empty) throw new ArgumentException("Stock transfer query is invalid.");
    }
}
