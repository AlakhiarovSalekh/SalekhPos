using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using SalekhPos.Billing.Application.Billing;
using SalekhPos.Billing.Contracts.Billing;
using SalekhPos.Billing.Domain.Accounts;
using SalekhPos.Billing.Domain.Invoices;

namespace SalekhPos.Billing.Infrastructure.Billing;

public sealed class PostgresBillingService(NpgsqlDataSource? source) : IBillingService
{
    public async Task<BillingAccountResponse> CreateAccountAsync(BillingIdentity identity, Guid organizationId,
        CreateBillingAccountRequest request, CancellationToken ct)
    {
        identity.Validate(); RequireIds(organizationId, request.OperationId, request.AccountId);
        var account = new BillingAccount(request.AccountId, organizationId, request.LegalName, request.BillingEmail,
            request.Currency, request.TaxIdentifier, DateTimeOffset.UtcNow);
        var hash = Hash(request);
        await using var c = await Open(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, organizationId, identity, ct); await Demand(c, t, organizationId, identity, "billing.manage", ct);
        await using var q = new NpgsqlCommand("""
            INSERT INTO billing.accounts(organization_id,account_id,operation_id,request_hash,legal_name,billing_email,currency,tax_identifier,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING account_id,legal_name,billing_email,currency,tax_identifier,created_at
            """, c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(account.Id); q.Parameters.AddWithValue(request.OperationId);
        q.Parameters.AddWithValue(hash); q.Parameters.AddWithValue(account.LegalName); q.Parameters.AddWithValue(account.BillingEmail);
        q.Parameters.AddWithValue(account.Currency); q.Parameters.AddWithValue((object?)account.TaxIdentifier ?? DBNull.Value);
        q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject);
        var result = await ReadAccount(q, ct);
        if (result is null)
        {
            await using var replay = new NpgsqlCommand("SELECT account_id,legal_name,billing_email,currency,tax_identifier,created_at,request_hash FROM billing.accounts WHERE organization_id=$1 AND operation_id=$2", c, t);
            replay.Parameters.AddWithValue(organizationId); replay.Parameters.AddWithValue(request.OperationId);
            await using var r = await replay.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct) || !Fixed(hash, r.GetString(6))) throw new BillingConflictException("Operation payload differs.");
            result = Account(r);
        }
        await t.CommitAsync(ct); return result;
    }

    public async Task<InvoiceResponse> CreateInvoiceAsync(BillingIdentity identity, Guid organizationId,
        CreateInvoiceRequest request, CancellationToken ct)
    {
        identity.Validate(); RequireIds(organizationId, request.OperationId, request.InvoiceId, request.AccountId);
        if (request.Lines is null or { Count: 0 } || request.Lines.Count > 500) throw new ArgumentException("Invoice lines are invalid.");
        var invoice = new Invoice(request.InvoiceId, organizationId, request.AccountId, request.Number, request.Currency, request.IssuedAt, request.DueAt);
        foreach (var x in request.Lines) invoice.AddLine(new InvoiceLine(x.LineId, x.Description, x.Quantity, x.UnitAmount, x.TaxRate));
        invoice.Open(); var hash = Hash(request);
        await using var c = await Open(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, organizationId, identity, ct); await Demand(c, t, organizationId, identity, "billing.manage", ct);
        await using var q = new NpgsqlCommand("""
            INSERT INTO billing.invoices(organization_id,invoice_id,account_id,operation_id,request_hash,invoice_number,currency,status,net_amount,tax_amount,gross_amount,issued_at,due_at,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,'open',$8,$9,$10,$11,$12,$13,$14) ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING invoice_id,account_id,invoice_number,currency,status,net_amount,tax_amount,gross_amount,paid_amount,issued_at,due_at
            """, c, t);
        AddInvoiceParameters(q, organizationId, request, invoice, hash, identity);
        var result = await ReadInvoice(q, ct);
        if (result is null)
        {
            var replay = await InvoiceByOperation(c, t, organizationId, request.OperationId, ct);
            if (replay.Response is null || !Fixed(hash, replay.Hash!)) throw new BillingConflictException("Operation payload differs.");
            await t.CommitAsync(ct); return replay.Response;
        }
        for (var i = 0; i < invoice.Lines.Count; i++)
        {
            var x = invoice.Lines[i]; await using var line = new NpgsqlCommand("""
                INSERT INTO billing.invoice_lines(organization_id,invoice_id,line_id,position,description,quantity,unit_amount,tax_rate,net_amount,tax_amount,gross_amount)
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
                """, c, t);
            line.Parameters.AddWithValue(organizationId); line.Parameters.AddWithValue(invoice.Id); line.Parameters.AddWithValue(x.Id);
            line.Parameters.AddWithValue(i + 1); line.Parameters.AddWithValue(x.Description); line.Parameters.AddWithValue(x.Quantity);
            line.Parameters.AddWithValue(x.UnitAmount); line.Parameters.AddWithValue(x.TaxRate); line.Parameters.AddWithValue(x.NetAmount);
            line.Parameters.AddWithValue(x.TaxAmount); line.Parameters.AddWithValue(x.GrossAmount); await line.ExecuteNonQueryAsync(ct);
        }
        await t.CommitAsync(ct); return result;
    }

    public async Task<InvoiceResponse> GetInvoiceAsync(BillingIdentity identity, Guid organizationId, Guid invoiceId, CancellationToken ct)
    {
        identity.Validate(); RequireIds(organizationId, invoiceId); await using var c = await Open(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, organizationId, identity, ct); await Demand(c, t, organizationId, identity, "billing.view", ct);
        await using var q = InvoiceSelect(c, t, "invoice_id=$2"); q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(invoiceId);
        var result = await ReadInvoice(q, ct) ?? throw new BillingNotFoundException(); await t.CommitAsync(ct); return result;
    }

    public async Task<ChargeResponse> CaptureChargeAsync(BillingIdentity identity, Guid organizationId, Guid invoiceId,
        CaptureChargeRequest request, CancellationToken ct)
    {
        identity.Validate(); RequireIds(organizationId, invoiceId, request.OperationId, request.ChargeId);
        _ = new Domain.Charges.Charge(request.ChargeId, organizationId, invoiceId, request.OperationId, request.Amount, request.Currency, DateTimeOffset.UtcNow);
        var provider = BillingAccount.Required(request.ProviderReference, 128, "Provider reference"); var hash = Hash(request);
        await using var c = await Open(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, organizationId, identity, ct); await Demand(c, t, organizationId, identity, "billing.manage", ct);
        var invoice = await LockedInvoice(c, t, organizationId, invoiceId, ct) ?? throw new BillingNotFoundException();
        if (!string.Equals(invoice.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase) || invoice.Balance < request.Amount || invoice.Status is "paid" or "voided") throw new BillingConflictException("Charge is not admissible.");
        await using var q = new NpgsqlCommand("""
            INSERT INTO billing.charges(organization_id,charge_id,invoice_id,operation_id,request_hash,amount,currency,status,provider_reference,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,'succeeded',$8,$9,$10) ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING charge_id,invoice_id,status,amount,currency,provider_reference
            """, c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(request.ChargeId); q.Parameters.AddWithValue(invoiceId); q.Parameters.AddWithValue(request.OperationId);
        q.Parameters.AddWithValue(hash); q.Parameters.AddWithValue(request.Amount); q.Parameters.AddWithValue(invoice.Currency); q.Parameters.AddWithValue(provider);
        q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject);
        var result = await ReadCharge(q, ct);
        if (result is null) result = await ReadChargeReplay(c, t, organizationId, request.OperationId, hash, ct);
        else { await using var update = new NpgsqlCommand("UPDATE billing.invoices SET paid_amount=paid_amount+$3,status=CASE WHEN paid_amount+$3=gross_amount THEN 'paid' ELSE 'partially_paid' END,row_version=row_version+1 WHERE organization_id=$1 AND invoice_id=$2", c, t); update.Parameters.AddWithValue(organizationId); update.Parameters.AddWithValue(invoiceId); update.Parameters.AddWithValue(request.Amount); await update.ExecuteNonQueryAsync(ct); }
        await t.CommitAsync(ct); return result;
    }

    public async Task<CreditResponse> IssueCreditAsync(BillingIdentity identity, Guid organizationId, Guid invoiceId,
        IssueCreditRequest request, CancellationToken ct)
    {
        identity.Validate(); RequireIds(organizationId, invoiceId, request.OperationId, request.CreditId);
        var credit = new Domain.Credits.CreditNote(request.CreditId, organizationId, invoiceId, request.OperationId, request.Amount, request.Currency, request.Reason, DateTimeOffset.UtcNow); var hash = Hash(request);
        await using var c = await Open(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, organizationId, identity, ct); await Demand(c, t, organizationId, identity, "billing.manage", ct);
        var invoice = await LockedInvoice(c, t, organizationId, invoiceId, ct) ?? throw new BillingNotFoundException();
        if (invoice.Currency != credit.Currency || request.Amount > invoice.GrossAmount - invoice.CreditedAmount) throw new BillingConflictException("Credit is not admissible.");
        await using var q = new NpgsqlCommand("""
            INSERT INTO billing.credit_notes(organization_id,credit_id,invoice_id,operation_id,request_hash,amount,currency,reason,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING credit_id,invoice_id,amount,currency,reason,issued_at
            """, c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(credit.Id); q.Parameters.AddWithValue(invoiceId); q.Parameters.AddWithValue(request.OperationId); q.Parameters.AddWithValue(hash); q.Parameters.AddWithValue(credit.Amount); q.Parameters.AddWithValue(credit.Currency); q.Parameters.AddWithValue(credit.Reason); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject);
        CreditResponse? result = null; await using (var r = await q.ExecuteReaderAsync(ct)) if (await r.ReadAsync(ct)) result = new(r.GetGuid(0), r.GetGuid(1), r.GetDecimal(2), r.GetString(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5));
        if (result is null) result = await ReadCreditReplay(c, t, organizationId, request.OperationId, hash, ct);
        else { await using var update = new NpgsqlCommand("UPDATE billing.invoices SET credited_amount=credited_amount+$3,row_version=row_version+1 WHERE organization_id=$1 AND invoice_id=$2", c, t); update.Parameters.AddWithValue(organizationId); update.Parameters.AddWithValue(invoiceId); update.Parameters.AddWithValue(request.Amount); await update.ExecuteNonQueryAsync(ct); }
        await t.CommitAsync(ct); return result;
    }

    private sealed record Locked(string Currency, string Status, decimal GrossAmount, decimal PaidAmount, decimal CreditedAmount) { public decimal Balance => GrossAmount - PaidAmount; }
    private async Task<NpgsqlConnection> Open(CancellationToken ct) => await (source ?? throw new BillingUnavailableException()).OpenConnectionAsync(ct);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    private static bool Fixed(string a, string b) => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
    private static void RequireIds(params Guid[] ids) { if (ids.Any(x => x == Guid.Empty)) throw new ArgumentException("Identifiers are required."); }
    private static async Task Prepare(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, BillingIdentity identity, CancellationToken ct)
    {
        await using var safety = new NpgsqlCommand("SELECT current_user='salekhpos_runtime' AND (SELECT relrowsecurity AND relforcerowsecurity FROM pg_class x JOIN pg_namespace n ON n.oid=x.relnamespace WHERE n.nspname='billing' AND x.relname='invoices')", c, t);
        if (await safety.ExecuteScalarAsync(ct) is not true) throw new BillingUnavailableException();
        await using var context = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t); context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer); context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(ct);
    }
    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, BillingIdentity identity, string permission, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("""SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$4 AND g.scope_kind='organization')""", c, t); q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject); q.Parameters.AddWithValue(permission); if (await q.ExecuteScalarAsync(ct) is not true) throw new BillingDeniedException();
    }
    private static BillingAccountResponse Account(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetFieldValue<DateTimeOffset>(5));
    private static async Task<BillingAccountResponse?> ReadAccount(NpgsqlCommand q, CancellationToken ct) { await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Account(r) : null; }
    private static InvoiceResponse Invoice(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7), r.GetDecimal(8), r.GetDecimal(7) - r.GetDecimal(8), r.GetFieldValue<DateTimeOffset>(9), r.GetFieldValue<DateTimeOffset>(10));
    private static async Task<InvoiceResponse?> ReadInvoice(NpgsqlCommand q, CancellationToken ct) { await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Invoice(r) : null; }
    private static NpgsqlCommand InvoiceSelect(NpgsqlConnection c, NpgsqlTransaction t, string where) => new($"SELECT invoice_id,account_id,invoice_number,currency,status,net_amount,tax_amount,gross_amount,paid_amount,issued_at,due_at FROM billing.invoices WHERE organization_id=$1 AND {where}", c, t);
    private static async Task<(InvoiceResponse? Response, string? Hash)> InvoiceByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid op, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT invoice_id,account_id,invoice_number,currency,status,net_amount,tax_amount,gross_amount,paid_amount,issued_at,due_at,request_hash FROM billing.invoices WHERE organization_id=$1 AND operation_id=$2", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(op); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? (Invoice(r), r.GetString(11)) : (null, null); }
    private static async Task<Locked?> LockedInvoice(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid id, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT currency,status,gross_amount,paid_amount,credited_amount FROM billing.invoices WHERE organization_id=$1 AND invoice_id=$2 FOR UPDATE", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(id); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetString(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4)) : null; }
    private static ChargeResponse Charge(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5));
    private static async Task<ChargeResponse?> ReadCharge(NpgsqlCommand q, CancellationToken ct) { await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Charge(r) : null; }
    private static async Task<ChargeResponse> ReadChargeReplay(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid op, string hash, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT charge_id,invoice_id,status,amount,currency,provider_reference,request_hash FROM billing.charges WHERE organization_id=$1 AND operation_id=$2", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(op); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct) || !Fixed(hash, r.GetString(6))) throw new BillingConflictException("Operation payload differs."); return Charge(r); }
    private static async Task<CreditResponse> ReadCreditReplay(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid op, string hash, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT credit_id,invoice_id,amount,currency,reason,issued_at,request_hash FROM billing.credit_notes WHERE organization_id=$1 AND operation_id=$2", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(op); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct) || !Fixed(hash, r.GetString(6))) throw new BillingConflictException("Operation payload differs."); return new(r.GetGuid(0), r.GetGuid(1), r.GetDecimal(2), r.GetString(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5)); }
    private static void AddInvoiceParameters(NpgsqlCommand q, Guid org, CreateInvoiceRequest request, Invoice invoice, string hash, BillingIdentity identity) { q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(invoice.Id); q.Parameters.AddWithValue(invoice.AccountId); q.Parameters.AddWithValue(request.OperationId); q.Parameters.AddWithValue(hash); q.Parameters.AddWithValue(invoice.Number); q.Parameters.AddWithValue(invoice.Currency); q.Parameters.AddWithValue(invoice.NetAmount); q.Parameters.AddWithValue(invoice.TaxAmount); q.Parameters.AddWithValue(invoice.GrossAmount); q.Parameters.AddWithValue(invoice.IssuedAt); q.Parameters.AddWithValue(invoice.DueAt); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject); }
}
