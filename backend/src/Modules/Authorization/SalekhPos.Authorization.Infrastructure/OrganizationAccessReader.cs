using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Authorization.Application;
using SalekhPos.Authorization.Contracts.Organizations;

namespace SalekhPos.Authorization.Infrastructure;

public sealed class OrganizationAccessReader(AccessDatabase database) : IAccessibleOrganizationReader
{
    public async Task<AccessibleOrganizationPage> ReadAsync(AccessIdentity identity, int pageSize,
        Guid? after, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (pageSize is < 1 or > 100 || after == Guid.Empty)
        {
            throw new ArgumentException("Invalid organization access query.");
        }

        var source = database.DataSource ?? throw new AccessUnavailableException();
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        if (!await AccessRuntimeSafety.IsSafeAsync(connection, transaction, cancellationToken))
        {
            throw new AccessUnavailableException();
        }

        await using var command = new NpgsqlCommand("""
            SELECT organization_id, name
            FROM access.list_accessible_organizations($1, $2, $3, $4)
            """, connection, transaction);
        command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
        command.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = (object?)after ?? DBNull.Value
        });
        command.Parameters.AddWithValue(pageSize + 1);

        var items = new List<AccessibleOrganizationResponse>(pageSize + 1);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new AccessibleOrganizationResponse(reader.GetGuid(0), reader.GetString(1)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        var nextCursor = items.Count > pageSize ? items[pageSize - 1].Id : (Guid?)null;
        if (items.Count > pageSize)
        {
            items.RemoveAt(pageSize);
        }

        return new AccessibleOrganizationPage(items.AsReadOnly(), nextCursor);
    }
}
