using Npgsql;

namespace SalekhPos.Authorization.Infrastructure;

internal static class AccessRuntimeSafety
{
    private const string Sql = """
        SELECT current_user = 'salekhpos_runtime'
            AND NOT EXISTS (SELECT FROM pg_roles WHERE rolname = current_user
                AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
            AND NOT EXISTS (SELECT FROM pg_auth_members WHERE member = (SELECT oid FROM pg_roles WHERE rolname = current_user))
            AND (SELECT count(*) = 6 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity
                    AND c.relowner <> (SELECT oid FROM pg_roles WHERE rolname = current_user))
                 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                 WHERE (n.nspname = 'organization' AND c.relname IN ('organizations','businesses','regions','branches'))
                    OR (n.nspname = 'access' AND c.relname IN ('memberships','permission_grants')))
            AND EXISTS (
                SELECT FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                JOIN pg_roles owner_role ON owner_role.oid = p.proowner
                WHERE n.nspname = 'access' AND p.proname = 'list_accessible_organizations'
                  AND pg_get_function_identity_arguments(p.oid) = 'p_issuer text, p_subject text, p_after uuid, p_limit integer'
                  AND p.prosecdef AND owner_role.rolname = 'salekhpos_access_reader'
                  AND NOT owner_role.rolcanlogin AND NOT owner_role.rolsuper AND NOT owner_role.rolbypassrls
                  AND has_function_privilege(current_user, p.oid, 'EXECUTE')
                  AND NOT EXISTS (SELECT FROM aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) acl
                      WHERE acl.grantee = 0 AND acl.privilege_type = 'EXECUTE'))
        """;

    internal static async Task<bool> IsSafeAsync(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(Sql, connection, transaction);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }
}
