using System.Text.Json;
using Npgsql;
using SalekhPos.SystemAdministration.Application;
using SalekhPos.SystemAdministration.Contracts;
using SalekhPos.SystemAdministration.Domain;

namespace SalekhPos.SystemAdministration.Infrastructure;

public sealed class PostgresSuperAdminRegistry(NpgsqlDataSource? source) : ISuperAdminRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string SafetySql = """
        SELECT current_user='salekhpos_runtime'
          AND NOT EXISTS(SELECT FROM pg_roles WHERE rolname=current_user
            AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
          AND NOT EXISTS(SELECT FROM pg_auth_members WHERE member=(SELECT oid FROM pg_roles WHERE rolname=current_user))
          AND NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_systemadministration'
            AND (rolcanlogin OR rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
          AND (SELECT count(*)=2 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity
                AND c.relowner=(SELECT oid FROM pg_roles WHERE rolname='salekhpos_systemadministration'))
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='system_administration' AND c.relname IN ('super_admins','authority_audit'))
          AND NOT has_schema_privilege(current_user,'system_administration','CREATE')
          AND NOT has_table_privilege(current_user,'system_administration.super_admins','SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
          AND NOT has_table_privilege(current_user,'system_administration.authority_audit','SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
          AND NOT has_any_column_privilege(current_user,'system_administration.super_admins','SELECT,INSERT,UPDATE,REFERENCES')
          AND NOT has_any_column_privilege(current_user,'system_administration.authority_audit','SELECT,INSERT,UPDATE,REFERENCES')
          AND NOT has_function_privilege(current_user,'system_administration.bootstrap_root(uuid,text,text,text,text)','EXECUTE')
        """;

    public Task<PlatformAuthority> GetAuthorityAsync(PlatformIdentity identity, CancellationToken cancellationToken) =>
        ExecuteAsync<PlatformAuthority>("SELECT system_administration.authority($1,$2)", [identity.Issuer, identity.Subject], cancellationToken);

    public Task<SuperAdminResponse> RegisterAsync(PrivilegedActor actor, Guid operationId, PlatformIdentity target,
        string reason, string traceId, CancellationToken cancellationToken) =>
        ExecuteAsync<SuperAdminResponse>("SELECT system_administration.register_super_admin($1,$2,$3,$4,$5,$6,$7,$8)",
            [operationId, actor.Identity.Issuer, actor.Identity.Subject, actor.HasMfa, actor.AuthenticatedAt.UtcDateTime, target.Subject, reason, traceId], cancellationToken);

    public Task<SuperAdminResponse> RevokeAsync(PrivilegedActor actor, Guid operationId, Guid targetId,
        string reason, string traceId, CancellationToken cancellationToken) =>
        ExecuteAsync<SuperAdminResponse>("SELECT system_administration.revoke_super_admin($1,$2,$3,$4,$5,$6,$7,$8)",
            [operationId, actor.Identity.Issuer, actor.Identity.Subject, actor.HasMfa, actor.AuthenticatedAt.UtcDateTime, targetId, reason, traceId], cancellationToken);

    private async Task<T> ExecuteAsync<T>(string sql, object[] parameters, CancellationToken cancellationToken)
    {
        if (source is null) { throw new PlatformUnavailableException(); }
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var safety = new NpgsqlCommand(SafetySql, connection, transaction))
        {
            if (await safety.ExecuteScalarAsync(cancellationToken) is not true) { throw new PlatformUnavailableException(); }
        }
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            foreach (var value in parameters) { command.Parameters.Add(new NpgsqlParameter { Value = value }); }
            var json = await command.ExecuteScalarAsync(cancellationToken) as string ?? throw new PlatformUnavailableException();
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new PlatformUnavailableException();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (PostgresException exception) when (exception.SqlState == "42501") { throw new PlatformAccessDeniedException(); }
        catch (PostgresException exception) when (exception.SqlState is "P0001" or "23505") { throw new PlatformConflictException(); }
    }
}
