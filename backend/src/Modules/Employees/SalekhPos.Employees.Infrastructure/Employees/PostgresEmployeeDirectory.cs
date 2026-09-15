using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Employees.Application.Employees;
using SalekhPos.Employees.Contracts.Employees;
using SalekhPos.Employees.Domain.Employees;

namespace SalekhPos.Employees.Infrastructure.Employees;

public sealed class PostgresEmployeeDirectory(NpgsqlDataSource? source) : IEmployeeDirectory
{
    public async Task<EmployeeWriteResult> CreateAsync(EmployeeIdentity identity,
        CreateEmployeeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (command.OperationId == Guid.Empty || command.BranchId == Guid.Empty)
            throw new ArgumentException("Employee create request is invalid.");
        var employee = command.ToEmployee();
        var dataSource = source ?? throw new EmployeeUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await PrepareDemand(connection, transaction, identity, employee.OrganizationId, command.BranchId,
            "employees.create", cancellationToken);
        var now = await DatabaseTime(connection, transaction, cancellationToken);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO employees.employees(organization_id,employee_id,operation_id,code,display_name,email,phone,
              job_title,is_active,row_version,created_at,updated_at,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,true,1,$9,$9,$10,$11)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING employee_id,code,display_name,email,phone,job_title,is_active,row_version,created_at,updated_at
            """, connection, transaction);
        insert.Parameters.AddWithValue(employee.OrganizationId); insert.Parameters.AddWithValue(employee.Id);
        insert.Parameters.AddWithValue(command.OperationId); insert.Parameters.AddWithValue(employee.Code);
        insert.Parameters.AddWithValue(employee.DisplayName);
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)employee.Email ?? DBNull.Value });
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)employee.Phone ?? DBNull.Value });
        insert.Parameters.AddWithValue(employee.JobTitle); insert.Parameters.AddWithValue(now);
        insert.Parameters.AddWithValue(identity.Issuer); insert.Parameters.AddWithValue(identity.Subject);
        EmployeeRow? row;
        try
        {
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            row = await reader.ReadAsync(cancellationToken) ? ReadRow(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new EmployeeConflictException(); }
        if (row is not null)
        {
            await InsertAssignment(connection, transaction, employee.OrganizationId, employee.Id,
                command.BranchId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ToResponse(row, command.BranchId), true);
        }
        await using var replay = new NpgsqlCommand($"""
            {EmployeeSelect} WHERE e.organization_id=$1 AND e.operation_id=$2 AND a.branch_id=$3
            """, connection, transaction);
        replay.Parameters.AddWithValue(employee.OrganizationId); replay.Parameters.AddWithValue(command.OperationId);
        replay.Parameters.AddWithValue(command.BranchId);
        await using var replayReader = await replay.ExecuteReaderAsync(cancellationToken);
        if (!await replayReader.ReadAsync(cancellationToken)) throw new EmployeeConflictException();
        var response = ReadResponse(replayReader);
        if (response.Id != employee.Id || response.Code != employee.Code || response.DisplayName != employee.DisplayName
            || response.Email != employee.Email || response.Phone != employee.Phone || response.JobTitle != employee.JobTitle)
            throw new EmployeeConflictException();
        await replayReader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(response, false);
    }

    public async Task<EmployeePage> ListAsync(EmployeeIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        ValidateQuery(identity, organizationId, branchId, pageSize, after);
        var dataSource = source ?? throw new EmployeeUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await PrepareDemand(connection, transaction, identity, organizationId, branchId,
            "employees.view", cancellationToken);
        await using var query = new NpgsqlCommand($"""
            {EmployeeSelect} WHERE e.organization_id=$1 AND a.branch_id=$2
              AND ($3::uuid IS NULL OR e.employee_id>$3) ORDER BY e.employee_id LIMIT $4
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Uuid, Value=(object?)after ?? DBNull.Value });
        query.Parameters.AddWithValue(pageSize + 1);
        var items = new List<EmployeeResponse>(pageSize + 1);
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) items.Add(ReadResponse(reader));
        Guid? next = null;
        if (items.Count > pageSize) { items.RemoveAt(pageSize); next = items[^1].Id; }
        await transaction.CommitAsync(cancellationToken);
        return new(items.AsReadOnly(), next);
    }

    public async Task<EmployeeResponse?> ReadAsync(EmployeeIdentity identity, Guid organizationId, Guid branchId,
        Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.");
        ValidateQuery(identity, organizationId, branchId, 1, null);
        var dataSource = source ?? throw new EmployeeUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await PrepareDemand(connection, transaction, identity, organizationId, branchId,
            "employees.view", cancellationToken);
        await using var query = new NpgsqlCommand($"""
            {EmployeeSelect} WHERE e.organization_id=$1 AND a.branch_id=$2 AND e.employee_id=$3
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(employeeId);
        EmployeeResponse? result;
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            result = await reader.ReadAsync(cancellationToken) ? ReadResponse(reader) : null;
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<EmployeeResponse> UpdateAsync(EmployeeIdentity identity, UpdateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OrganizationId == Guid.Empty || command.BranchId == Guid.Empty || command.EmployeeId == Guid.Empty
            || command.ExpectedVersion < 1) throw new ArgumentException("Employee update is invalid.");
        var validated = new Employee(command.OrganizationId, command.EmployeeId, "VALID", command.DisplayName,
            command.Email, command.Phone, command.JobTitle, command.IsActive);
        var dataSource = source ?? throw new EmployeeUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await PrepareDemand(connection, transaction, identity, command.OrganizationId, command.BranchId,
            "employees.update", cancellationToken);
        if (!await HasAssignment(connection, transaction, command.OrganizationId, command.EmployeeId,
                command.BranchId, cancellationToken)) throw new EmployeeNotFoundException();
        var now = await DatabaseTime(connection, transaction, cancellationToken);
        await using var update = new NpgsqlCommand("""
            UPDATE employees.employees SET display_name=$3,email=$4,phone=$5,job_title=$6,is_active=$7,
              row_version=row_version+1,updated_at=$8
            WHERE organization_id=$1 AND employee_id=$2 AND row_version=$9
            RETURNING employee_id,code,display_name,email,phone,job_title,is_active,row_version,created_at,updated_at
            """, connection, transaction);
        update.Parameters.AddWithValue(command.OrganizationId); update.Parameters.AddWithValue(command.EmployeeId);
        update.Parameters.AddWithValue(validated.DisplayName);
        update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Text, Value=(object?)validated.Email ?? DBNull.Value });
        update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Text, Value=(object?)validated.Phone ?? DBNull.Value });
        update.Parameters.AddWithValue(validated.JobTitle); update.Parameters.AddWithValue(validated.IsActive);
        update.Parameters.AddWithValue(now); update.Parameters.AddWithValue(command.ExpectedVersion);
        EmployeeRow? row;
        await using (var reader = await update.ExecuteReaderAsync(cancellationToken))
            row = await reader.ReadAsync(cancellationToken) ? ReadRow(reader) : null;
        if (row is null)
        {
            await using var exists = new NpgsqlCommand(
                "SELECT EXISTS(SELECT FROM employees.employees WHERE organization_id=$1 AND employee_id=$2)",
                connection, transaction);
            exists.Parameters.AddWithValue(command.OrganizationId); exists.Parameters.AddWithValue(command.EmployeeId);
            if (await exists.ExecuteScalarAsync(cancellationToken) is true) throw new EmployeeConflictException();
            throw new EmployeeNotFoundException();
        }
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(row, command.BranchId);
    }

    private static async Task InsertAssignment(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid employeeId, Guid branchId, DateTimeOffset assignedAt,
        CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand("""
            INSERT INTO employees.store_assignments(organization_id,employee_id,branch_id,assigned_at)
            VALUES($1,$2,$3,$4)
            """, connection, transaction);
        insert.Parameters.AddWithValue(organizationId); insert.Parameters.AddWithValue(employeeId);
        insert.Parameters.AddWithValue(branchId); insert.Parameters.AddWithValue(assignedAt);
        try { await insert.ExecuteNonQueryAsync(cancellationToken); }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new EmployeeConflictException(); }
    }

    private static async Task<bool> HasAssignment(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid employeeId, Guid branchId, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM employees.store_assignments
              WHERE organization_id=$1 AND employee_id=$2 AND branch_id=$3)
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(employeeId);
        query.Parameters.AddWithValue(branchId);
        return await query.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task PrepareDemand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        EmployeeIdentity identity, Guid organizationId, Guid branchId, string permission,
        CancellationToken cancellationToken)
    {
        await using (var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='employees' AND c.relname='employees' AND c.relrowsecurity
                  AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction))
            if (await safety.ExecuteScalarAsync(cancellationToken) is not true) throw new EmployeeUnavailableException();
        await using (var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction))
        {
            context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer);
            context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(cancellationToken);
        }
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
        if (await demand.ExecuteScalarAsync(cancellationToken) is not true) throw new EmployeeDeniedException();
    }

    private static void ValidateQuery(EmployeeIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (organizationId == Guid.Empty || branchId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty)
            throw new ArgumentException("Employee query is invalid.");
    }

    private static async Task<DateTimeOffset> DatabaseTime(NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT statement_timestamp()", connection, transaction);
        return await command.ExecuteScalarAsync(cancellationToken) is DateTimeOffset value
            ? value : throw new EmployeeUnavailableException();
    }

    private const string EmployeeSelect = """
        SELECT e.employee_id,a.branch_id,e.code,e.display_name,e.email,e.phone,e.job_title,e.is_active,
          e.row_version,e.created_at,e.updated_at FROM employees.employees e
        JOIN employees.store_assignments a ON a.organization_id=e.organization_id AND a.employee_id=e.employee_id
        """;

    private static EmployeeResponse ReadResponse(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1),
        reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6), reader.GetBoolean(7),
        reader.GetInt64(8), reader.GetFieldValue<DateTimeOffset>(9), reader.GetFieldValue<DateTimeOffset>(10));

    private static EmployeeRow ReadRow(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1),
        reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetString(5), reader.GetBoolean(6), reader.GetInt64(7), reader.GetFieldValue<DateTimeOffset>(8),
        reader.GetFieldValue<DateTimeOffset>(9));

    private static EmployeeResponse ToResponse(EmployeeRow row, Guid branchId) => new(row.Id, branchId, row.Code,
        row.DisplayName, row.Email, row.Phone, row.JobTitle, row.IsActive, row.Version, row.CreatedAt, row.UpdatedAt);

    private sealed record EmployeeRow(Guid Id, string Code, string DisplayName, string? Email, string? Phone,
        string JobTitle, bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}
