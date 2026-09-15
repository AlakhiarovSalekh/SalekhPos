using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Suppliers.Application.Suppliers;
using SalekhPos.Suppliers.Contracts.Suppliers;
using SalekhPos.Suppliers.Domain.Suppliers;

namespace SalekhPos.Suppliers.Infrastructure.Suppliers;

public sealed class PostgresSupplierDirectory(NpgsqlDataSource? source) : ISupplierDirectory
{
    public async Task<SupplierWriteResult> CreateAsync(SupplierIdentity identity,
        CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(command);
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
        var supplier = command.ToSupplier();
        var dataSource = source ?? throw new SupplierUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Prepare(connection, transaction, supplier.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, supplier.OrganizationId, identity, "suppliers.create", cancellationToken);

        await using var insert = new NpgsqlCommand("""
            INSERT INTO suppliers.suppliers(organization_id,supplier_id,operation_id,code,name,tax_id,email,phone,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING supplier_id,code,name,tax_id,email,phone,is_active,row_version,created_at,updated_at
            """, connection, transaction);
        insert.Parameters.AddWithValue(supplier.OrganizationId);
        insert.Parameters.AddWithValue(supplier.Id);
        insert.Parameters.AddWithValue(command.OperationId);
        insert.Parameters.AddWithValue(supplier.Code);
        insert.Parameters.AddWithValue(supplier.Name);
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)supplier.TaxId ?? DBNull.Value });
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)supplier.Email ?? DBNull.Value });
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)supplier.Phone ?? DBNull.Value });
        insert.Parameters.AddWithValue(identity.Issuer);
        insert.Parameters.AddWithValue(identity.Subject);
        SupplierResponse? response;
        try
        {
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            response = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new SupplierConflictException();
        }
        if (response is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(response, true);
        }

        await using var replay = new NpgsqlCommand("""
            SELECT supplier_id,code,name,tax_id,email,phone,is_active,row_version,created_at,updated_at
            FROM suppliers.suppliers WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        replay.Parameters.AddWithValue(supplier.OrganizationId);
        replay.Parameters.AddWithValue(command.OperationId);
        await using var replayReader = await replay.ExecuteReaderAsync(cancellationToken);
        if (!await replayReader.ReadAsync(cancellationToken)) throw new SupplierUnavailableException();
        response = Read(replayReader);
        if (response.Code != supplier.Code || response.Name != supplier.Name || response.TaxId != supplier.TaxId
            || response.Email != supplier.Email || response.Phone != supplier.Phone)
            throw new SupplierConflictException();
        await replayReader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(response, false);
    }

    public async Task<SupplierPage> ListAsync(SupplierIdentity identity, Guid organizationId, int pageSize,
        Guid? after, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty)
            throw new ArgumentException("Supplier query is invalid.");
        var dataSource = source ?? throw new SupplierUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "suppliers.view", cancellationToken);
        await using var query = new NpgsqlCommand("""
            SELECT supplier_id,code,name,tax_id,email,phone,is_active,row_version,created_at,updated_at
            FROM suppliers.suppliers
            WHERE organization_id=$1 AND ($2::uuid IS NULL OR supplier_id>$2)
            ORDER BY supplier_id LIMIT $3
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = (object?)after ?? DBNull.Value });
        query.Parameters.AddWithValue(pageSize + 1);
        var items = new List<SupplierResponse>(pageSize + 1);
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) items.Add(Read(reader));
        Guid? next = null;
        if (items.Count > pageSize)
        {
            items.RemoveAt(pageSize);
            next = items[^1].Id;
        }
        await transaction.CommitAsync(cancellationToken);
        return new(items.AsReadOnly(), next);
    }

    public async Task<SupplierResponse?> ReadAsync(SupplierIdentity identity, Guid organizationId,
        Guid supplierId, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || supplierId == Guid.Empty)
            throw new ArgumentException("Supplier lookup is invalid.");
        var dataSource = source ?? throw new SupplierUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "suppliers.view", cancellationToken);
        await using var query = new NpgsqlCommand("""
            SELECT supplier_id,code,name,tax_id,email,phone,is_active,row_version,created_at,updated_at
            FROM suppliers.suppliers WHERE organization_id=$1 AND supplier_id=$2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(supplierId);
        SupplierResponse? result;
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            result = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<SupplierResponse> UpdateAsync(SupplierIdentity identity, UpdateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OrganizationId == Guid.Empty || command.SupplierId == Guid.Empty || command.ExpectedVersion < 1)
            throw new ArgumentException("Supplier update is invalid.");
        var validated = new Supplier(command.OrganizationId, command.SupplierId, "VALID", command.Name, command.TaxId,
            command.Email, command.Phone, command.IsActive);
        var dataSource = source ?? throw new SupplierUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, command.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, command.OrganizationId, identity, "suppliers.update", cancellationToken);
        await using var update = new NpgsqlCommand("""
            UPDATE suppliers.suppliers SET name=$3,tax_id=$4,email=$5,phone=$6,is_active=$7,
              row_version=row_version+1,updated_at=statement_timestamp()
            WHERE organization_id=$1 AND supplier_id=$2 AND row_version=$8
            RETURNING supplier_id,code,name,tax_id,email,phone,is_active,row_version,created_at,updated_at
            """, connection, transaction);
        update.Parameters.AddWithValue(command.OrganizationId);
        update.Parameters.AddWithValue(command.SupplierId);
        update.Parameters.AddWithValue(validated.Name);
        update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)validated.TaxId ?? DBNull.Value });
        update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)validated.Email ?? DBNull.Value });
        update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)validated.Phone ?? DBNull.Value });
        update.Parameters.AddWithValue(validated.IsActive);
        update.Parameters.AddWithValue(command.ExpectedVersion);
        SupplierResponse? result;
        try
        {
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            result = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new SupplierConflictException();
        }
        if (result is null)
        {
            await using var exists = new NpgsqlCommand(
                "SELECT EXISTS(SELECT FROM suppliers.suppliers WHERE organization_id=$1 AND supplier_id=$2)",
                connection, transaction);
            exists.Parameters.AddWithValue(command.OrganizationId);
            exists.Parameters.AddWithValue(command.SupplierId);
            if (await exists.ExecuteScalarAsync(cancellationToken) is true) throw new SupplierConflictException();
            throw new SupplierNotFoundException();
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, SupplierIdentity identity, CancellationToken cancellationToken)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='suppliers' AND c.relname='suppliers' AND c.relrowsecurity
                  AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(cancellationToken) is not true) throw new SupplierUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString());
        context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject);
        await context.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, SupplierIdentity identity, string permission, CancellationToken cancellationToken)
    {
        await using var demand = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m JOIN organization.organizations o USING(organization_id)
              JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active AND o.is_active
                AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission=$4 AND g.scope_kind='organization')
            """, connection, transaction);
        demand.Parameters.AddWithValue(organizationId);
        demand.Parameters.AddWithValue(identity.Issuer);
        demand.Parameters.AddWithValue(identity.Subject);
        demand.Parameters.AddWithValue(permission);
        if (await demand.ExecuteScalarAsync(cancellationToken) is not true) throw new SupplierDeniedException();
    }

    private static SupplierResponse Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetBoolean(6), reader.GetInt64(7),
        reader.GetFieldValue<DateTimeOffset>(8), reader.GetFieldValue<DateTimeOffset>(9));
}
