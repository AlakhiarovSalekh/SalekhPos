using Microsoft.Extensions.Options;
using Npgsql;
using SalekhPos.Authorization.Infrastructure;
using SalekhPos.Integrations.Application;

namespace SalekhPos.Worker.Integrations;

public sealed class PostgresWebhookPayloadSource(
    AccessDatabase database,
    IOptions<WebhookDispatchOptions> options) : IWebhookPayloadSource
{
    private readonly WebhookDispatchOptions settings = options.Value;

    public bool Supports(Uri reference) =>
        string.Equals(reference.Scheme, "pgpayload", StringComparison.OrdinalIgnoreCase);

    public async ValueTask<ReadOnlyMemory<byte>> ResolveAsync(Uri reference, CancellationToken cancellationToken)
    {
        if (!Supports(reference)
            || !string.IsNullOrEmpty(reference.Query)
            || !string.IsNullOrEmpty(reference.Fragment)
            || !string.IsNullOrEmpty(reference.UserInfo)
            || !Guid.TryParse(reference.Host, out var organizationId)
            || organizationId == Guid.Empty
            || !Guid.TryParse(reference.AbsolutePath.Trim('/'), out var deliveryId)
            || deliveryId == Guid.Empty)
        {
            throw new ArgumentException("PostgreSQL webhook payload reference is invalid.", nameof(reference));
        }

        var identity = new IntegrationIdentity(settings.Issuer, settings.Subject);
        identity.Validate();
        var source = database.DataSource ?? throw new IntegrationUnavailableException();

        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND EXISTS(
                SELECT FROM pg_class x
                JOIN pg_namespace n ON n.oid=x.relnamespace
                WHERE n.nspname='integrations' AND x.relname='webhook_payloads'
                  AND x.relrowsecurity AND x.relforcerowsecurity
                  AND x.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction))
        {
            if (await safety.ExecuteScalarAsync(cancellationToken) is not true)
            {
                throw new IntegrationUnavailableException();
            }
        }

        await using (var context = new NpgsqlCommand("""
            SELECT set_config('app.organization_id',$1,true),
              set_config('app.issuer',$2,true),
              set_config('app.subject',$3,true)
            """, connection, transaction))
        {
            context.Parameters.AddWithValue(organizationId.ToString());
            context.Parameters.AddWithValue(identity.Issuer);
            context.Parameters.AddWithValue(identity.Subject);
            await context.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var query = new NpgsqlCommand("""
            SELECT p.payload
            FROM integrations.webhook_payloads p
            WHERE p.organization_id=$1 AND p.delivery_id=$2
              AND EXISTS(
                SELECT FROM access.memberships m
                JOIN access.permission_grants g
                  ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
                WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4
                  AND m.is_active AND m.valid_from<=statement_timestamp()
                  AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                  AND g.permission='integrations.dispatch' AND g.scope_kind='organization')
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(deliveryId);
        query.Parameters.AddWithValue(identity.Issuer);
        query.Parameters.AddWithValue(identity.Subject);
        var payload = await query.ExecuteScalarAsync(cancellationToken) as byte[]
            ?? throw new IntegrationNotFoundException();
        await transaction.CommitAsync(cancellationToken);

        if (payload.Length is < 1 or > WebhookTransport.MaximumPayloadBytes)
        {
            throw new IntegrationUnavailableException();
        }

        return payload;
    }
}
