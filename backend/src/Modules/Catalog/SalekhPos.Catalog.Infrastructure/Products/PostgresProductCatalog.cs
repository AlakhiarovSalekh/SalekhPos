using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Catalog.Application.Products;
using SalekhPos.Catalog.Contracts.Products;

namespace SalekhPos.Catalog.Infrastructure.Products;

public sealed class PostgresProductCatalog(NpgsqlDataSource? source) : IProductCatalog
{
    public async Task<ProductWriteResult> CreateAsync(CatalogIdentity identity, CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(command);
        var product = command.ToProduct();
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID must not be empty.");
        var dataSource = source ?? throw new CatalogUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await DemandSafeRuntime(connection, transaction, cancellationToken);
        await SetContext(connection, transaction, product.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, product.OrganizationId, identity, "products.create", true, cancellationToken);

        await using var insert = new NpgsqlCommand("""
            INSERT INTO catalog.products(organization_id, product_id, operation_id, sku, name, unit_code, barcode)
            VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT (organization_id, operation_id) DO NOTHING
            RETURNING product_id, sku, name, unit_code, barcode, is_active, row_version
            """, connection, transaction);
        insert.Parameters.AddWithValue(product.OrganizationId);
        insert.Parameters.AddWithValue(product.Id);
        insert.Parameters.AddWithValue(command.OperationId);
        insert.Parameters.AddWithValue(product.Sku);
        insert.Parameters.AddWithValue(product.Name);
        insert.Parameters.AddWithValue(product.UnitCode);
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)product.Barcode ?? DBNull.Value });
        ProductResponse? response;
        try
        {
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            response = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ProductConflictException();
        }
        if (response is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(response, true);
        }

        await using var replay = new NpgsqlCommand("""
            SELECT product_id, sku, name, unit_code, barcode, is_active, row_version
            FROM catalog.products WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        replay.Parameters.AddWithValue(product.OrganizationId);
        replay.Parameters.AddWithValue(command.OperationId);
        await using var replayReader = await replay.ExecuteReaderAsync(cancellationToken);
        if (!await replayReader.ReadAsync(cancellationToken)) throw new CatalogUnavailableException();
        response = Read(replayReader);
        if (response.Sku != product.Sku || response.Name != product.Name
            || response.UnitCode != product.UnitCode || response.Barcode != product.Barcode)
            throw new ProductConflictException();
        await replayReader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(response, false);
    }

    public async Task<ProductPage> ReadAsync(CatalogIdentity identity, Guid organizationId, int pageSize,
        Guid? after, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty)
            throw new ArgumentException("Product query is invalid.");
        var dataSource = source ?? throw new CatalogUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await DemandSafeRuntime(connection, transaction, cancellationToken);
        await SetContext(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "products.view", false, cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT product_id, sku, name, unit_code, barcode, is_active, row_version
            FROM catalog.products WHERE organization_id=$1 AND ($2::uuid IS NULL OR product_id > $2)
            ORDER BY product_id LIMIT $3
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)after ?? DBNull.Value });
        command.Parameters.AddWithValue(pageSize + 1);
        var products = new List<ProductResponse>(pageSize + 1);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) products.Add(Read(reader));
        await transaction.CommitAsync(cancellationToken);
        var next = products.Count > pageSize ? products[pageSize - 1].Id : (Guid?)null;
        if (products.Count > pageSize) products.RemoveAt(pageSize);
        return new(products.AsReadOnly(), next);
    }

    public async Task<ProductResponse?> ReadOneAsync(CatalogIdentity identity, Guid organizationId,
        Guid? productId, string? barcode, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || (productId is null) == (barcode is null) || productId == Guid.Empty)
            throw new ArgumentException("Product lookup is invalid.");
        if (barcode is not null)
            _ = new SalekhPos.Catalog.Domain.Products.Product(organizationId, Guid.NewGuid(), "VALID", "Valid", "EA", barcode);
        var dataSource = source ?? throw new CatalogUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await DemandSafeRuntime(connection, transaction, cancellationToken);
        await SetContext(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "products.view", false, cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT product_id, sku, name, unit_code, barcode, is_active, row_version
            FROM catalog.products WHERE organization_id=$1
              AND (($2::uuid IS NOT NULL AND product_id=$2) OR ($3::text IS NOT NULL AND barcode=$3))
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)productId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)barcode ?? DBNull.Value });
        ProductResponse? response;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            response = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<ProductResponse> UpdateAsync(CatalogIdentity identity, UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OrganizationId == Guid.Empty || command.ProductId == Guid.Empty || command.ExpectedVersion < 1)
            throw new ArgumentException("Product update is invalid.");
        var validated = new SalekhPos.Catalog.Domain.Products.Product(command.OrganizationId, command.ProductId,
            "VALID", command.Name, command.UnitCode, command.Barcode, command.IsActive);
        var dataSource = source ?? throw new CatalogUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await DemandSafeRuntime(connection, transaction, cancellationToken);
        await SetContext(connection, transaction, command.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, command.OrganizationId, identity, "products.update", true, cancellationToken);
        await using var update = new NpgsqlCommand("""
            UPDATE catalog.products SET name=$3,unit_code=$4,barcode=$5,is_active=$6,
              row_version=row_version+1,updated_at=statement_timestamp()
            WHERE organization_id=$1 AND product_id=$2 AND row_version=$7
            RETURNING product_id,sku,name,unit_code,barcode,is_active,row_version
            """, connection, transaction);
        update.Parameters.AddWithValue(command.OrganizationId);
        update.Parameters.AddWithValue(command.ProductId);
        update.Parameters.AddWithValue(validated.Name);
        update.Parameters.AddWithValue(validated.UnitCode);
        update.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)validated.Barcode ?? DBNull.Value });
        update.Parameters.AddWithValue(validated.IsActive);
        update.Parameters.AddWithValue(command.ExpectedVersion);
        ProductResponse? response;
        try
        {
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            response = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ProductConflictException();
        }
        if (response is null)
        {
            await using var exists = new NpgsqlCommand(
                "SELECT EXISTS(SELECT FROM catalog.products WHERE organization_id=$1 AND product_id=$2)", connection, transaction);
            exists.Parameters.AddWithValue(command.OrganizationId);
            exists.Parameters.AddWithValue(command.ProductId);
            if (await exists.ExecuteScalarAsync(cancellationToken) is true) throw new ProductConflictException();
            throw new ProductNotFoundException();
        }
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private static async Task SetContext(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId,
        CatalogIdentity identity, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true), set_config('app.issuer',$2,true), set_config('app.subject',$3,true)", connection, transaction);
        command.Parameters.AddWithValue(organizationId.ToString());
        command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DemandSafeRuntime(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime'
              AND NOT EXISTS(SELECT FROM pg_roles WHERE rolname=current_user
                AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
              AND NOT EXISTS(SELECT FROM pg_auth_members WHERE member=(SELECT oid FROM pg_roles WHERE rolname=current_user))
              AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='catalog' AND c.relname='products' AND c.relrowsecurity
                  AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true) throw new CatalogUnavailableException();
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId,
        CatalogIdentity identity, string permission, bool organizationOnly, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m JOIN organization.organizations o USING(organization_id)
              JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active AND o.is_active
                AND m.valid_from <= statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until > statement_timestamp())
                AND g.permission=$4 AND (NOT $5 OR g.scope_kind='organization'))
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
        command.Parameters.AddWithValue(permission);
        command.Parameters.AddWithValue(organizationOnly);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true) throw new CatalogDeniedException();
    }

    private static ProductResponse Read(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1),
        reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetBoolean(5), reader.GetInt64(6));
}
