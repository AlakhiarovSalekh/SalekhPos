using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Sales.Application.CompleteSale;
using SalekhPos.Sales.Contracts.CompleteSale;
using SalekhPos.Sales.Domain.SaleItems;
using SalekhPos.Sales.Domain.Sales;

namespace SalekhPos.Sales.Infrastructure.CompleteSale;

public sealed class PostgresCashSaleCompletion(NpgsqlDataSource? source) : ICashSaleCompletion, ISaleReader
{
    public async Task<CompletedSaleResponse?> ReadAsync(SalesIdentity identity, Guid organizationId, Guid branchId,
        Guid saleId, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || saleId == Guid.Empty)
            throw new ArgumentException("Sale query is invalid.");
        var dataSource = source ?? throw new SalesUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureSafeRuntime(connection, transaction, cancellationToken);
        await SetContext(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, "sales.view", cancellationToken);
        var response = await ReadById(connection, transaction, organizationId, branchId, saleId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<CashSaleWriteResult> CompleteAsync(SalesIdentity identity, CompleteCashSaleCommand command,
        CancellationToken cancellationToken)
    {
        Validate(command);
        var dataSource = source ?? throw new SalesUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await EnsureSafeRuntime(connection, transaction, cancellationToken);
        await SetContext(connection, transaction, command.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, command.OrganizationId, command.BranchId, identity,
            "sales.complete", cancellationToken);
        await Lock(connection, transaction, $"sale:{command.OrganizationId:D}:{command.OperationId:D}", cancellationToken);
        var replay = await ReadByOperation(connection, transaction, command.OrganizationId, command.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (!SameRequest(replay, command)) throw new SalesConflictException();
            await transaction.CommitAsync(cancellationToken);
            return new(replay, false);
        }

        foreach (var productId in command.Lines.Select(line => line.ProductId).Order())
            await Lock(connection, transaction, $"stock:{command.OrganizationId:D}:{command.BranchId:D}:{productId:D}", cancellationToken);
        var completedAt = await DatabaseTime(connection, transaction, cancellationToken);
        var calculations = new List<SaleLineCalculation>(command.Lines.Count);
        foreach (var requested in command.Lines)
        {
            var (priceId, amount, currency, mode, rate) = await ResolvePrice(connection, transaction, command.OrganizationId, command.BranchId,
                requested.ProductId, completedAt, cancellationToken) ?? throw new SalePriceUnavailableException();
            var stock = await Stock(connection, transaction, command.OrganizationId, command.BranchId,
                requested.ProductId, cancellationToken);
            if (stock < requested.Quantity) throw new InsufficientStockException();
            calculations.Add(new SaleLineCalculation(requested.ProductId, priceId, requested.Quantity,
                amount, currency, mode, rate));
        }
        var sale = new CompletedSale(command.OrganizationId, command.BranchId, command.SaleId, calculations,
            command.CashReceived, completedAt);
        await InsertSale(connection, transaction, sale, command.OperationId, identity, cancellationToken);
        var responses = new List<CompletedSaleLineResponse>(calculations.Count);
        for (var index = 0; index < calculations.Count; index++)
        {
            var line = calculations[index];
            var movementId = Guid.NewGuid();
            var movementOperationId = Guid.NewGuid();
            await InsertMovement(connection, transaction, sale, line, movementId, movementOperationId, identity, cancellationToken);
            await InsertLine(connection, transaction, sale, line, index + 1, movementId, movementOperationId, cancellationToken);
            responses.Add(Response(line, index + 1));
        }
        await InsertOutbox(connection, transaction, sale, responses, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ToResponse(sale, responses), true);
    }

    private static void Validate(CompleteCashSaleCommand command)
    {
        if (command.OrganizationId == Guid.Empty || command.BranchId == Guid.Empty || command.SaleId == Guid.Empty
            || command.OperationId == Guid.Empty || command.Lines is null || command.Lines.Count is < 1 or > 500
            || command.Lines.Any(line => line.ProductId == Guid.Empty || line.Quantity <= 0
                || decimal.Round(line.Quantity, 6) != line.Quantity)
            || command.Lines.Select(line => line.ProductId).Distinct().Count() != command.Lines.Count)
            throw new ArgumentException("Sale request is invalid.");
    }

    private static async Task EnsureSafeRuntime(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime' AND NOT EXISTS(SELECT FROM pg_roles
              WHERE rolname=current_user AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
              AND NOT EXISTS(SELECT FROM pg_auth_members WHERE member=(SELECT oid FROM pg_roles WHERE rolname=current_user))
              AND (SELECT bool_and(c.relrowsecurity AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
                FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='sales' AND c.relname IN('completed_sales','sale_lines','outbox_messages'))
            """, connection, transaction);
        if (await query.ExecuteScalarAsync(cancellationToken) is not true) throw new SalesUnavailableException();
    }

    private static async Task SetContext(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId,
        SalesIdentity identity, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", connection, transaction);
        query.Parameters.AddWithValue(organizationId.ToString()); query.Parameters.AddWithValue(identity.Issuer);
        query.Parameters.AddWithValue(identity.Subject); await query.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, SalesIdentity identity, string permission,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g
              ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
            JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2
            JOIN organization.businesses business ON business.organization_id=b.organization_id AND business.business_id=b.business_id
            JOIN organization.organizations organization ON organization.organization_id=b.organization_id
            WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active AND b.is_active
              AND business.is_active AND organization.is_active AND m.valid_from<=statement_timestamp()
              AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$5
              AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
                OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
                OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(identity.Issuer); query.Parameters.AddWithValue(identity.Subject);
        query.Parameters.AddWithValue(permission);
        if (await query.ExecuteScalarAsync(cancellationToken) is not true) throw new SalesDeniedException();
    }

    private static async Task Lock(NpgsqlConnection connection, NpgsqlTransaction transaction, string key,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction);
        query.Parameters.AddWithValue(key); await query.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<DateTimeOffset> DatabaseTime(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("SELECT statement_timestamp()", connection, transaction);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new SalesUnavailableException();
        return reader.GetFieldValue<DateTimeOffset>(0);
    }

    private static async Task<(Guid Id, decimal Amount, string Currency, AppliedTaxMode Mode, decimal Rate)?> ResolvePrice(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId, Guid branchId,
        Guid productId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT price.price_id,price.amount,price.currency,price.tax_mode,price.tax_rate
            FROM pricing.prices price JOIN catalog.products product
              ON product.organization_id=price.organization_id AND product.product_id=price.product_id AND product.is_active
            WHERE price.organization_id=$1 AND price.product_id=$2 AND (price.branch_id=$3 OR price.branch_id IS NULL)
              AND price.valid_from<=$4 AND (price.valid_until IS NULL OR price.valid_until>$4)
            ORDER BY price.branch_id IS NOT NULL DESC,price.valid_from DESC LIMIT 1
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(productId);
        query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(at);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return (reader.GetGuid(0), reader.GetDecimal(1), reader.GetString(2),
            reader.GetString(3) == "inclusive" ? AppliedTaxMode.Inclusive : AppliedTaxMode.Exclusive, reader.GetDecimal(4));
    }

    private static async Task<decimal> Stock(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, Guid productId, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("SELECT coalesce(sum(direction*quantity),0)::numeric(20,6) FROM inventory.stock_movements WHERE organization_id=$1 AND branch_id=$2 AND product_id=$3", connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(productId);
        return (decimal)(await query.ExecuteScalarAsync(cancellationToken) ?? 0m);
    }

    private static async Task InsertSale(NpgsqlConnection connection, NpgsqlTransaction transaction, CompletedSale sale,
        Guid operationId, SalesIdentity identity, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            INSERT INTO sales.completed_sales(organization_id,sale_id,operation_id,branch_id,currency,net_total,
              tax_total,grand_total,cash_received,change_due,completed_at,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
            """, connection, transaction);
        query.Parameters.AddWithValue(sale.OrganizationId); query.Parameters.AddWithValue(sale.Id);
        query.Parameters.AddWithValue(operationId); query.Parameters.AddWithValue(sale.BranchId);
        query.Parameters.AddWithValue(sale.Currency); query.Parameters.AddWithValue(sale.NetTotal);
        query.Parameters.AddWithValue(sale.TaxTotal); query.Parameters.AddWithValue(sale.GrandTotal);
        query.Parameters.AddWithValue(sale.CashReceived); query.Parameters.AddWithValue(sale.ChangeDue);
        query.Parameters.AddWithValue(sale.CompletedAt); query.Parameters.AddWithValue(identity.Issuer);
        query.Parameters.AddWithValue(identity.Subject); await query.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertMovement(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CompletedSale sale, SaleLineCalculation line, Guid movementId, Guid operationId, SalesIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movements(organization_id,movement_id,operation_id,branch_id,product_id,
              kind,direction,quantity,reason,occurred_at,issuer,subject)
            VALUES($1,$2,$3,$4,$5,'sale',-1,$6,'Completed cash sale',$7,$8,$9)
            """, connection, transaction);
        query.Parameters.AddWithValue(sale.OrganizationId); query.Parameters.AddWithValue(movementId);
        query.Parameters.AddWithValue(operationId); query.Parameters.AddWithValue(sale.BranchId);
        query.Parameters.AddWithValue(line.ProductId); query.Parameters.AddWithValue(line.Quantity);
        query.Parameters.AddWithValue(sale.CompletedAt); query.Parameters.AddWithValue(identity.Issuer);
        query.Parameters.AddWithValue(identity.Subject); await query.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertLine(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CompletedSale sale, SaleLineCalculation line, int number, Guid movementId, Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            INSERT INTO sales.sale_lines(organization_id,sale_id,line_number,product_id,price_id,inventory_movement_id,
              inventory_operation_id,quantity,unit_amount,currency,tax_mode,tax_rate,net_amount,tax_amount,gross_amount)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
            """, connection, transaction);
        query.Parameters.AddWithValue(sale.OrganizationId); query.Parameters.AddWithValue(sale.Id);
        query.Parameters.AddWithValue(number); query.Parameters.AddWithValue(line.ProductId);
        query.Parameters.AddWithValue(line.PriceId); query.Parameters.AddWithValue(movementId);
        query.Parameters.AddWithValue(operationId); query.Parameters.AddWithValue(line.Quantity);
        query.Parameters.AddWithValue(line.UnitAmount); query.Parameters.AddWithValue(line.Currency);
        query.Parameters.AddWithValue(line.TaxMode == AppliedTaxMode.Inclusive ? "inclusive" : "exclusive");
        query.Parameters.AddWithValue(line.TaxRate); query.Parameters.AddWithValue(line.NetAmount);
        query.Parameters.AddWithValue(line.TaxAmount); query.Parameters.AddWithValue(line.GrossAmount);
        await query.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOutbox(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CompletedSale sale, IReadOnlyList<CompletedSaleLineResponse> lines, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            saleId = sale.Id,
            organizationId = sale.OrganizationId,
            branchId = sale.BranchId,
            sale.Currency,
            sale.GrandTotal,
            sale.CompletedAt,
            lines
        });
        await using var query = new NpgsqlCommand("INSERT INTO sales.outbox_messages(organization_id,message_id,sale_id,event_type,payload,occurred_at) VALUES($1,$2,$3,'sales.sale_completed.v1',$4,$5)", connection, transaction);
        query.Parameters.AddWithValue(sale.OrganizationId); query.Parameters.AddWithValue(Guid.NewGuid());
        query.Parameters.AddWithValue(sale.Id); query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = payload });
        query.Parameters.AddWithValue(sale.CompletedAt); await query.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<CompletedSaleResponse?> ReadByOperation(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid operationId, CancellationToken cancellationToken)
    {
        await using var saleQuery = new NpgsqlCommand("SELECT sale_id,branch_id,currency,net_total,tax_total,grand_total,cash_received,change_due,completed_at FROM sales.completed_sales WHERE organization_id=$1 AND operation_id=$2", connection, transaction);
        saleQuery.Parameters.AddWithValue(organizationId); saleQuery.Parameters.AddWithValue(operationId);
        Guid id; Guid branch; string currency; decimal net; decimal tax; decimal grand; decimal cash; decimal change; DateTimeOffset at;
        await using (var reader = await saleQuery.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            id = reader.GetGuid(0); branch = reader.GetGuid(1); currency = reader.GetString(2); net = reader.GetDecimal(3);
            tax = reader.GetDecimal(4); grand = reader.GetDecimal(5); cash = reader.GetDecimal(6);
            change = reader.GetDecimal(7); at = reader.GetFieldValue<DateTimeOffset>(8);
        }
        await using var lineQuery = new NpgsqlCommand("SELECT line_number,product_id,price_id,quantity,unit_amount,currency,tax_mode,tax_rate,net_amount,tax_amount,gross_amount FROM sales.sale_lines WHERE organization_id=$1 AND sale_id=$2 ORDER BY line_number", connection, transaction);
        lineQuery.Parameters.AddWithValue(organizationId); lineQuery.Parameters.AddWithValue(id);
        var lines = new List<CompletedSaleLineResponse>();
        await using (var reader = await lineQuery.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) lines.Add(new(reader.GetInt32(0), reader.GetGuid(1),
                reader.GetGuid(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6),
                reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10)));
        return new(id, branch, currency, net, tax, grand, cash, change, at, lines.AsReadOnly());
    }

    private static async Task<CompletedSaleResponse?> ReadById(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid branchId, Guid saleId,
        CancellationToken cancellationToken)
    {
        await using var saleQuery = new NpgsqlCommand("SELECT sale_id,branch_id,currency,net_total,tax_total,grand_total,cash_received,change_due,completed_at FROM sales.completed_sales WHERE organization_id=$1 AND branch_id=$2 AND sale_id=$3", connection, transaction);
        saleQuery.Parameters.AddWithValue(organizationId); saleQuery.Parameters.AddWithValue(branchId);
        saleQuery.Parameters.AddWithValue(saleId);
        Guid id; string currency; decimal net; decimal tax; decimal grand; decimal cash; decimal change; DateTimeOffset at;
        await using (var reader = await saleQuery.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            id = reader.GetGuid(0); currency = reader.GetString(2); net = reader.GetDecimal(3);
            tax = reader.GetDecimal(4); grand = reader.GetDecimal(5); cash = reader.GetDecimal(6);
            change = reader.GetDecimal(7); at = reader.GetFieldValue<DateTimeOffset>(8);
        }
        await using var lineQuery = new NpgsqlCommand("SELECT line_number,product_id,price_id,quantity,unit_amount,currency,tax_mode,tax_rate,net_amount,tax_amount,gross_amount FROM sales.sale_lines WHERE organization_id=$1 AND sale_id=$2 ORDER BY line_number", connection, transaction);
        lineQuery.Parameters.AddWithValue(organizationId); lineQuery.Parameters.AddWithValue(id);
        var lines = new List<CompletedSaleLineResponse>();
        await using (var reader = await lineQuery.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) lines.Add(new(reader.GetInt32(0), reader.GetGuid(1),
                reader.GetGuid(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6),
                reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10)));
        return new(id, branchId, currency, net, tax, grand, cash, change, at, lines.AsReadOnly());
    }

    private static bool SameRequest(CompletedSaleResponse sale, CompleteCashSaleCommand command) =>
        sale.BranchId == command.BranchId && sale.CashReceived == command.CashReceived && sale.Lines.Count == command.Lines.Count
        && sale.Lines.Zip(command.Lines).All(pair => pair.First.ProductId == pair.Second.ProductId
            && pair.First.Quantity == pair.Second.Quantity);
    private static CompletedSaleLineResponse Response(SaleLineCalculation line, int number) => new(number, line.ProductId,
        line.PriceId, line.Quantity, line.UnitAmount, line.Currency,
        line.TaxMode == AppliedTaxMode.Inclusive ? "inclusive" : "exclusive", line.TaxRate,
        line.NetAmount, line.TaxAmount, line.GrossAmount);
    private static CompletedSaleResponse ToResponse(CompletedSale sale, IReadOnlyList<CompletedSaleLineResponse> lines) =>
        new(sale.Id, sale.BranchId, sale.Currency, sale.NetTotal, sale.TaxTotal, sale.GrandTotal,
            sale.CashReceived, sale.ChangeDue, sale.CompletedAt, lines);
}
