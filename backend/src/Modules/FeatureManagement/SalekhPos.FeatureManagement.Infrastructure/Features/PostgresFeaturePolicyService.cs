using Npgsql;
using SalekhPos.FeatureManagement.Application.Features;
using SalekhPos.FeatureManagement.Contracts.Features;
using SalekhPos.FeatureManagement.Domain.Features;

namespace SalekhPos.FeatureManagement.Infrastructure.Features;

public sealed class PostgresFeaturePolicyService(NpgsqlDataSource? source) : IFeaturePolicyService
{
    public async Task<FeatureDecisionResponse> EvaluateAsync(FeatureIdentity identity,
        Guid organizationId, string key, CancellationToken cancellationToken)
    {
        Validate(identity, organizationId, key);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Context(connection, transaction, identity, organizationId, cancellationToken);
        await Demand(connection, transaction, identity, organizationId, "features.view", cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT f.default_enabled,f.emergency_disabled,f.rollout_percentage,o.enabled,
                   COALESCE(e.enabled,true)
            FROM feature_management.features f
            LEFT JOIN feature_management.organization_overrides o
              ON o.organization_id=$1 AND o.feature_key=f.feature_key
            LEFT JOIN feature_management.organization_entitlements e
              ON e.organization_id=$1 AND e.feature_key=f.feature_key
            WHERE f.feature_key=$2
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.AddWithValue(key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new FeatureNotFoundException();
        var policy = new FeaturePolicy(key, reader.GetBoolean(0), reader.GetBoolean(1), reader.GetInt32(2));
        bool? tenantOverride = reader.IsDBNull(3) ? null : reader.GetBoolean(3);
        var entitled = reader.GetBoolean(4);
        var enabled = policy.Evaluate(organizationId, tenantOverride, entitled);
        var sourceName = policy.EmergencyDisabled ? "emergency_disabled"
            : tenantOverride.HasValue ? "tenant_override"
            : entitled ? "policy" : "entitlement";
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(key, enabled, sourceName, DateTimeOffset.UtcNow);
    }

    public async Task<FeatureOverrideResponse> SetOverrideAsync(FeatureIdentity identity,
        Guid organizationId, string key, bool enabled, string reason,
        CancellationToken cancellationToken)
    {
        Validate(identity, organizationId, key);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || reason.Any(char.IsControl))
            throw new ArgumentException("Override reason is invalid.");
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Context(connection, transaction, identity, organizationId, cancellationToken);
        await Demand(connection, transaction, identity, organizationId, "features.manage", cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO feature_management.organization_overrides(
              organization_id,feature_key,enabled,reason,updated_by_issuer,updated_by_subject)
            SELECT $1,feature_key,$3,$4,$5,$6 FROM feature_management.features WHERE feature_key=$2
            ON CONFLICT(organization_id,feature_key) DO UPDATE SET enabled=excluded.enabled,
              reason=excluded.reason,updated_by_issuer=excluded.updated_by_issuer,
              updated_by_subject=excluded.updated_by_subject,updated_at=statement_timestamp()
            RETURNING updated_at
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.AddWithValue(key);
        command.Parameters.AddWithValue(enabled);
        command.Parameters.AddWithValue(reason.Trim());
        command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
        var updated = await command.ExecuteScalarAsync(cancellationToken);
        if (updated is not DateTimeOffset at) throw new FeatureNotFoundException();
        await transaction.CommitAsync(cancellationToken);
        return new(organizationId, key, enabled, reason.Trim(), at);
    }

    private async Task<NpgsqlConnection> Open(CancellationToken cancellationToken) =>
        await (source ?? throw new FeatureUnavailableException()).OpenConnectionAsync(cancellationToken);

    private static void Validate(FeatureIdentity identity, Guid organizationId, string key)
    {
        identity.Validate();
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        _ = new FeaturePolicy(key, false, false, 0).Validate();
    }

    private static async Task Context(NpgsqlConnection connection, NpgsqlTransaction transaction,
        FeatureIdentity identity, Guid organizationId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        command.Parameters.AddWithValue(organizationId.ToString());
        command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        FeatureIdentity identity, Guid organizationId, string permission,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g
              ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active
                AND m.valid_from<=statement_timestamp()
                AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission=$4 AND g.scope_kind='organization')
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
        command.Parameters.AddWithValue(permission);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
            throw new FeatureDeniedException();
    }
}
