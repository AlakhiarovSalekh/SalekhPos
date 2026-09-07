using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Access.Application;

namespace SalekhPos.Access.Infrastructure;

public sealed class BranchAccessReader(AccessDatabase database)
{
    // The same check protects readiness AND each business read. A accidentally
    // elevated connection must never bypass RLS just because readiness was ignored.
    private const string RuntimeSafetySql = """
        SELECT current_user = 'salekhpos_runtime'
            AND NOT EXISTS (SELECT FROM pg_roles WHERE rolname = current_user
                AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
            AND NOT EXISTS (SELECT FROM pg_auth_members WHERE member = (SELECT oid FROM pg_roles WHERE rolname = current_user))
            AND (SELECT count(*) = 6 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity
                    AND c.relowner <> (SELECT oid FROM pg_roles WHERE rolname = current_user))
                 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                 WHERE (n.nspname = 'organization' AND c.relname IN ('organizations','businesses','regions','branches'))
                    OR (n.nspname = 'access' AND c.relname IN ('memberships','permission_grants')))
        """;

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        if (database.DataSource is null)
        {
            return false;
        }
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        return await HasSafeRuntimeAsync(connection, null, cancellationToken);
    }

    public async Task<BranchPage> ReadAsync(AccessIdentity identity, Guid organizationId,
        int pageSize, Guid? after, Guid? branchId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (organizationId == Guid.Empty || pageSize is < 1 or > 100
            || after == Guid.Empty || branchId == Guid.Empty)
        {
            throw new ArgumentException("Invalid branch query.");
        }
        var source = database.DataSource ?? throw new AccessUnavailableException();
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (!await HasSafeRuntimeAsync(connection, transaction, cancellationToken))
        {
            throw new AccessUnavailableException();
        }

        // Target organization is only a requested filter. Identity RLS, active
        // membership and persisted effective grants must all authorize it below.
        await using (var context = new NpgsqlCommand("""
            SELECT set_config('app.organization_id', $1, true),
                   set_config('app.issuer', $2, true), set_config('app.subject', $3, true)
            """, connection, transaction))
        {
            context.Parameters.AddWithValue(organizationId.ToString());
            context.Parameters.AddWithValue(identity.Issuer);
            context.Parameters.AddWithValue(identity.Subject);
            await context.ExecuteNonQueryAsync(cancellationToken);
        }

        // Join membership and grants into the actual data query as well, so a
        // revocation committed between statements cannot expose branch data.
        const string eligibility = """
            m.organization_id = $1 AND m.issuer = $2 AND m.subject = $3
            AND m.is_active AND m.valid_from <= statement_timestamp()
            AND (m.valid_until IS NULL OR m.valid_until > statement_timestamp())
            AND o.is_active AND g.permission = 'branches.view'
            """;
        await using (var authorize = new NpgsqlCommand("""
            SELECT EXISTS (SELECT FROM access.memberships m
                JOIN organization.organizations o ON o.organization_id = m.organization_id
                JOIN access.permission_grants g ON g.organization_id = m.organization_id AND g.membership_id = m.membership_id
                WHERE
            """ + "\n" + eligibility + ")", connection, transaction))
        {
            AddIdentity(authorize, organizationId, identity);
            if (await authorize.ExecuteScalarAsync(cancellationToken) is not true)
            {
                throw new AccessDeniedException();
            }
        }

        await using var command = new NpgsqlCommand("""
            SELECT b.branch_id, b.business_id, b.region_id, b.code, b.name, b.time_zone_id
            FROM organization.branches b
            JOIN organization.organizations o ON o.organization_id = b.organization_id
            JOIN organization.businesses business ON business.organization_id = b.organization_id AND business.business_id = b.business_id
            LEFT JOIN organization.regions region ON region.organization_id = b.organization_id AND region.business_id = b.business_id AND region.region_id = b.region_id
            WHERE b.organization_id = $1 AND b.is_active AND b.is_configured AND business.is_active
              AND (b.region_id IS NULL OR region.is_active)
              AND ($4::uuid IS NULL OR b.branch_id > $4)
              AND ($5::uuid IS NULL OR b.branch_id = $5)
              AND EXISTS (SELECT FROM access.memberships m JOIN access.permission_grants g
                    ON g.organization_id = m.organization_id AND g.membership_id = m.membership_id
                  WHERE
            """ + "\n" + eligibility + "\n" + """
                  AND (g.scope_kind = 'organization'
                    OR (g.scope_kind = 'business' AND g.business_id = b.business_id)
                    OR (g.scope_kind = 'region' AND g.business_id = b.business_id AND g.region_id = b.region_id)
                    OR (g.scope_kind = 'branch' AND g.business_id = b.business_id AND g.branch_id = b.branch_id)))
            ORDER BY b.branch_id LIMIT $6
            """, connection, transaction);
        AddIdentity(command, organizationId, identity);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)after ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)branchId ?? DBNull.Value });
        command.Parameters.AddWithValue(pageSize + 1);
        var items = new List<BranchSummary>(pageSize + 1);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new BranchSummary(reader.GetGuid(0), reader.GetGuid(1),
                    reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
            }
        }
        await transaction.CommitAsync(cancellationToken);
        Guid? nextCursor = items.Count > pageSize ? items[pageSize - 1].Id : null;
        if (items.Count > pageSize)
        {
            items.RemoveAt(pageSize);
        }
        return new BranchPage(items.AsReadOnly(), nextCursor);
    }

    private static void AddIdentity(NpgsqlCommand command, Guid organizationId, AccessIdentity identity)
    {
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
    }

    private static async Task<bool> HasSafeRuntimeAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(RuntimeSafetySql, connection, transaction);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }
}
