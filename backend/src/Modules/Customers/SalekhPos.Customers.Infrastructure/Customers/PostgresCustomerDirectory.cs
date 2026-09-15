using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Customers.Application.Customers;
using SalekhPos.Customers.Contracts.Customers;
using SalekhPos.Customers.Domain.Customers;

namespace SalekhPos.Customers.Infrastructure.Customers;

public sealed class PostgresCustomerDirectory(NpgsqlDataSource? source) : ICustomerDirectory
{
    public async Task<CustomerWriteResult> CreateAsync(CustomerIdentity identity,
        CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(command);
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
        var customer = command.ToCustomer();
        var dataSource = source ?? throw new CustomerUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Prepare(connection, transaction, customer.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, customer.OrganizationId, identity, "customers.create", cancellationToken);

        await using var insert = new NpgsqlCommand("""
            INSERT INTO customers.customers(organization_id,customer_id,operation_id,code,display_name,email,phone,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING customer_id,code,display_name,email,phone,is_active,row_version,created_at,updated_at
            """, connection, transaction);
        insert.Parameters.AddWithValue(customer.OrganizationId);
        insert.Parameters.AddWithValue(customer.Id);
        insert.Parameters.AddWithValue(command.OperationId);
        insert.Parameters.AddWithValue(customer.Code);
        insert.Parameters.AddWithValue(customer.DisplayName);
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)customer.Email ?? DBNull.Value });
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)customer.Phone ?? DBNull.Value });
        insert.Parameters.AddWithValue(identity.Issuer);
        insert.Parameters.AddWithValue(identity.Subject);
        CustomerResponse? response;
        try
        {
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            response = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new CustomerConflictException();
        }
        if (response is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(response, true);
        }

        await using var replay = new NpgsqlCommand("""
            SELECT customer_id,code,display_name,email,phone,is_active,row_version,created_at,updated_at
            FROM customers.customers WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        replay.Parameters.AddWithValue(customer.OrganizationId);
        replay.Parameters.AddWithValue(command.OperationId);
        await using var replayReader = await replay.ExecuteReaderAsync(cancellationToken);
        if (!await replayReader.ReadAsync(cancellationToken)) throw new CustomerUnavailableException();
        response = Read(replayReader);
        if (response.Code != customer.Code || response.DisplayName != customer.DisplayName
            || response.Email != customer.Email || response.Phone != customer.Phone)
            throw new CustomerConflictException();
        await replayReader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(response, false);
    }

    public async Task<CustomerPage> ListAsync(CustomerIdentity identity, Guid organizationId, int pageSize,
        Guid? after, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty)
            throw new ArgumentException("Customer query is invalid.");
        var dataSource = source ?? throw new CustomerUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "customers.view", cancellationToken);
        await using var query = new NpgsqlCommand("""
            SELECT customer_id,code,display_name,email,phone,is_active,row_version,created_at,updated_at
            FROM customers.customers
            WHERE organization_id=$1 AND ($2::uuid IS NULL OR customer_id>$2)
            ORDER BY customer_id LIMIT $3
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = (object?)after ?? DBNull.Value });
        query.Parameters.AddWithValue(pageSize + 1);
        var items = new List<CustomerResponse>(pageSize + 1);
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

    public async Task<CustomerResponse?> ReadAsync(CustomerIdentity identity, Guid organizationId,
        Guid customerId, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || customerId == Guid.Empty)
            throw new ArgumentException("Customer lookup is invalid.");
        var dataSource = source ?? throw new CustomerUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "customers.view", cancellationToken);
        await using var query = new NpgsqlCommand("""
            SELECT customer_id,code,display_name,email,phone,is_active,row_version,created_at,updated_at
            FROM customers.customers WHERE organization_id=$1 AND customer_id=$2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(customerId);
        CustomerResponse? result;
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            result = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<CustomerResponse> UpdateAsync(CustomerIdentity identity, UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OrganizationId == Guid.Empty || command.CustomerId == Guid.Empty || command.ExpectedVersion < 1)
            throw new ArgumentException("Customer update is invalid.");
        var validated = new Customer(command.OrganizationId, command.CustomerId, "VALID", command.DisplayName,
            command.Email, command.Phone, command.IsActive);
        var dataSource = source ?? throw new CustomerUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, command.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, command.OrganizationId, identity, "customers.update", cancellationToken);
        await using var update = new NpgsqlCommand("""
            UPDATE customers.customers SET display_name=$3,email=$4,phone=$5,is_active=$6,
              row_version=row_version+1,updated_at=statement_timestamp()
            WHERE organization_id=$1 AND customer_id=$2 AND row_version=$7
            RETURNING customer_id,code,display_name,email,phone,is_active,row_version,created_at,updated_at
            """, connection, transaction);
        update.Parameters.AddWithValue(command.OrganizationId);
        update.Parameters.AddWithValue(command.CustomerId);
        update.Parameters.AddWithValue(validated.DisplayName);
        update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)validated.Email ?? DBNull.Value });
        update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)validated.Phone ?? DBNull.Value });
        update.Parameters.AddWithValue(validated.IsActive);
        update.Parameters.AddWithValue(command.ExpectedVersion);
        CustomerResponse? result;
        try
        {
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            result = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new CustomerConflictException();
        }
        if (result is null)
        {
            await using var exists = new NpgsqlCommand(
                "SELECT EXISTS(SELECT FROM customers.customers WHERE organization_id=$1 AND customer_id=$2)",
                connection, transaction);
            exists.Parameters.AddWithValue(command.OrganizationId);
            exists.Parameters.AddWithValue(command.CustomerId);
            if (await exists.ExecuteScalarAsync(cancellationToken) is true) throw new CustomerConflictException();
            throw new CustomerNotFoundException();
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, CustomerIdentity identity, CancellationToken cancellationToken)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='customers' AND c.relname='customers' AND c.relrowsecurity
                  AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(cancellationToken) is not true) throw new CustomerUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString());
        context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject);
        await context.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, CustomerIdentity identity, string permission, CancellationToken cancellationToken)
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
        if (await demand.ExecuteScalarAsync(cancellationToken) is not true) throw new CustomerDeniedException();
    }

    private static CustomerResponse Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetBoolean(5), reader.GetInt64(6), reader.GetFieldValue<DateTimeOffset>(7),
        reader.GetFieldValue<DateTimeOffset>(8));
}
