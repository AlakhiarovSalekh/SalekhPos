using Npgsql;
using SalekhPos.Identity.Application.Sessions;

namespace SalekhPos.Identity.Infrastructure.Tokens;

// A shared connection pool is composed by the host; this module only accesses
// its own schema. Database ownership and authorization remain module-local.
public sealed class TokenRevocations(NpgsqlDataSource? source) : ITokenRevocations
{
    private const string SafetySql = """
        SELECT current_user = 'salekhpos_runtime'
          AND NOT EXISTS (SELECT FROM pg_roles WHERE rolname = current_user
            AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
          AND NOT EXISTS (SELECT FROM pg_auth_members WHERE member = (SELECT oid FROM pg_roles WHERE rolname = current_user))
          AND EXISTS (SELECT FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'identity' AND c.relname = 'revoked_tokens'
              AND c.relrowsecurity AND c.relforcerowsecurity
              AND c.relowner <> (SELECT oid FROM pg_roles WHERE rolname = current_user))
          AND has_table_privilege(current_user, 'identity.revoked_tokens', 'SELECT')
          AND has_column_privilege(current_user, 'identity.revoked_tokens', 'fingerprint', 'INSERT')
          AND has_column_privilege(current_user, 'identity.revoked_tokens', 'issuer', 'INSERT')
          AND has_column_privilege(current_user, 'identity.revoked_tokens', 'subject', 'INSERT')
          AND has_column_privilege(current_user, 'identity.revoked_tokens', 'expires_at', 'INSERT')
          AND has_column_privilege(current_user, 'identity.revoked_tokens', 'trace_id', 'INSERT')
          AND NOT has_column_privilege(current_user, 'identity.revoked_tokens', 'revoked_at', 'INSERT')
          AND NOT has_column_privilege(current_user, 'identity.revoked_tokens', 'action', 'INSERT')
          AND NOT has_table_privilege(current_user, 'identity.revoked_tokens', 'UPDATE,DELETE,TRUNCATE')
        """;

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        if (source is null) { return false; }
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(SafetySql, connection);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public Task<bool> IsRevokedAsync(AuthenticatedCredential credential, CancellationToken cancellationToken) =>
        ExecuteAsync(credential, null, cancellationToken);

    public async Task RevokeAsync(AuthenticatedCredential credential, string traceId, CancellationToken cancellationToken)
    {
        if (traceId.Length != 32 || traceId.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("Invalid trace identifier.", nameof(traceId));
        }
        await ExecuteAsync(credential, traceId, cancellationToken);
    }

    private async Task<bool> ExecuteAsync(AuthenticatedCredential credential, string? traceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (source is null) { throw new IdentityUnavailableException(); }
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var safety = new NpgsqlCommand(SafetySql, connection, transaction))
        {
            if (await safety.ExecuteScalarAsync(cancellationToken) is not true)
            {
                throw new IdentityUnavailableException();
            }
        }
        await using (var context = new NpgsqlCommand("""
            SELECT set_config('app.issuer', $1, true), set_config('app.subject', $2, true)
            """, connection, transaction))
        {
            context.Parameters.AddWithValue(credential.Issuer);
            context.Parameters.AddWithValue(credential.Subject);
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        var sql = traceId is null
            ? "SELECT EXISTS (SELECT FROM identity.revoked_tokens WHERE fingerprint=$1 AND issuer=$2 AND subject=$3)"
            : """
              INSERT INTO identity.revoked_tokens(fingerprint,issuer,subject,expires_at,trace_id)
              VALUES($1,$2,$3,$4,$5) ON CONFLICT (fingerprint) DO NOTHING
              RETURNING true
              """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(Convert.FromHexString(credential.Fingerprint));
        command.Parameters.AddWithValue(credential.Issuer);
        command.Parameters.AddWithValue(credential.Subject);
        if (traceId is not null)
        {
            command.Parameters.AddWithValue(credential.ExpiresAtUtc);
            command.Parameters.AddWithValue(traceId);
        }
        var result = await command.ExecuteScalarAsync(cancellationToken) is true;
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
