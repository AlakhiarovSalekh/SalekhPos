using Npgsql;
using NpgsqlTypes;
using SalekhPos.Taxation.Application.TaxConfiguration;
using SalekhPos.Taxation.Contracts.TaxConfiguration;
using SalekhPos.Taxation.Domain.TaxProfiles;
using SalekhPos.Taxation.Domain.TaxRates;

namespace SalekhPos.Taxation.Infrastructure.TaxConfiguration;

public sealed class PostgresTaxConfiguration(NpgsqlDataSource? source) : ITaxConfiguration
{
    private sealed record ProfileRow(Guid Id, string Code, string Name, string CountryCode, bool Inclusive, bool Active, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
    private sealed record RateRow(Guid Id, Guid ProfileId, Guid? BranchId, string Category, decimal Rate, DateTimeOffset From, DateTimeOffset? Until, bool Active, long Version);

    public async Task<TaxProfileWriteResult> CreateProfileAsync(TaxIdentity identity, CreateTaxProfileCommand command, CancellationToken ct)
    {
        identity.Validate(); if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required."); var profile = command.ToProfile();
        var data = source ?? throw new TaxUnavailableException(); await using var c = await data.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, profile.OrganizationId, identity, ct); await Demand(c, t, profile.OrganizationId, null, identity, "taxation.manage", ct);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO taxation.tax_profiles(organization_id,profile_id,operation_id,code,name,country_code,prices_include_tax,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING profile_id,code,name,country_code,prices_include_tax,is_active,row_version,created_at,updated_at
            """, c, t);
        insert.Parameters.AddWithValue(profile.OrganizationId); insert.Parameters.AddWithValue(profile.Id); insert.Parameters.AddWithValue(command.OperationId);
        insert.Parameters.AddWithValue(profile.Code); insert.Parameters.AddWithValue(profile.Name); insert.Parameters.AddWithValue(profile.CountryCode);
        insert.Parameters.AddWithValue(profile.PricesIncludeTax); insert.Parameters.AddWithValue(identity.Issuer); insert.Parameters.AddWithValue(identity.Subject);
        ProfileRow? row = null; await using (var r = await insert.ExecuteReaderAsync(ct)) if (await r.ReadAsync(ct)) row = ReadProfile(r);
        var created = row is not null;
        if (row is null) { row = await ReadProfileByOperation(c, t, profile.OrganizationId, command.OperationId, ct) ?? throw new TaxUnavailableException(); if (row.Code != profile.Code || row.Name != profile.Name || row.CountryCode != profile.CountryCode || row.Inclusive != profile.PricesIncludeTax) throw new TaxConflictException(); }
        await t.CommitAsync(ct); return new(ToProfileResponse(row), created);
    }
    public async Task<TaxProfilePage> ListProfilesAsync(TaxIdentity identity, Guid organizationId, int pageSize, Guid? after, CancellationToken ct)
    {
        identity.Validate(); if (organizationId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty) throw new ArgumentException("Tax query is invalid.");
        var data = source ?? throw new TaxUnavailableException(); await using var c = await data.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, organizationId, identity, ct); await Demand(c, t, organizationId, null, identity, "taxation.view", ct);
        await using var q = new NpgsqlCommand("""
            SELECT profile_id,code,name,country_code,prices_include_tax,is_active,row_version,created_at,updated_at
            FROM taxation.tax_profiles WHERE organization_id=$1 AND ($2::uuid IS NULL OR profile_id>$2)
            ORDER BY profile_id LIMIT $3
            """, c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = after.HasValue ? after.Value : DBNull.Value }); q.Parameters.AddWithValue(pageSize + 1);
        var rows = new List<ProfileRow>(); await using (var r = await q.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) rows.Add(ReadProfile(r));
        Guid? next = null; if (rows.Count > pageSize) { rows.RemoveAt(pageSize); next = rows[^1].Id; }
        await t.CommitAsync(ct); return new([.. rows.Select(ToProfileResponse)], next);
    }
    public async Task<TaxRateWriteResult> CreateRateAsync(TaxIdentity identity, CreateTaxRateCommand command, CancellationToken ct)
    {
        identity.Validate(); if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required."); var rate = command.ToRate();
        var data = source ?? throw new TaxUnavailableException(); await using var c = await data.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, rate.OrganizationId, identity, ct); await Demand(c, t, rate.OrganizationId, rate.BranchId, identity, "taxation.manage", ct);
        await EnsureProfile(c, t, rate.OrganizationId, rate.ProfileId, ct); if (rate.BranchId.HasValue) await EnsureBranch(c, t, rate.OrganizationId, rate.BranchId.Value, ct);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO taxation.tax_rates(organization_id,rate_id,operation_id,profile_id,branch_id,category_code,rate_percent,effective_from,effective_until)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING rate_id,profile_id,branch_id,category_code,rate_percent,effective_from,effective_until,is_active,row_version
            """, c, t);
        insert.Parameters.AddWithValue(rate.OrganizationId); insert.Parameters.AddWithValue(rate.Id); insert.Parameters.AddWithValue(command.OperationId); insert.Parameters.AddWithValue(rate.ProfileId);
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = rate.BranchId.HasValue ? rate.BranchId.Value : DBNull.Value }); insert.Parameters.AddWithValue(rate.CategoryCode); insert.Parameters.AddWithValue(rate.RatePercent); insert.Parameters.AddWithValue(rate.EffectiveFrom);
        insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = rate.EffectiveUntil.HasValue ? rate.EffectiveUntil.Value : DBNull.Value });
        RateRow? row = null; await using (var r = await insert.ExecuteReaderAsync(ct)) if (await r.ReadAsync(ct)) row = ReadRate(r); var created = row is not null;
        row ??= await ReadRateByOperation(c, t, rate.OrganizationId, command.OperationId, ct) ?? throw new TaxUnavailableException();
        await t.CommitAsync(ct); return new(ToRateResponse(row), created);
    }
    public async Task<IReadOnlyList<TaxRateResponse>> ListRatesAsync(TaxIdentity identity, Guid organizationId, Guid profileId, CancellationToken ct)
    {
        identity.Validate(); if (organizationId == Guid.Empty || profileId == Guid.Empty) throw new ArgumentException("Tax query is invalid.");
        var data = source ?? throw new TaxUnavailableException(); await using var c = await data.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, organizationId, identity, ct); await Demand(c, t, organizationId, null, identity, "taxation.view", ct);
        await using var q = new NpgsqlCommand("""
            SELECT rate_id,profile_id,branch_id,category_code,rate_percent,effective_from,effective_until,is_active,row_version
            FROM taxation.tax_rates WHERE organization_id=$1 AND profile_id=$2 ORDER BY category_code,effective_from DESC,rate_id
            """, c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(profileId); var rows = new List<TaxRateResponse>();
        await using (var r = await q.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) rows.Add(ToRateResponse(ReadRate(r)));
        await t.CommitAsync(ct); return rows;
    }
    public async Task<CalculateTaxResponse> CalculateAsync(TaxIdentity identity, Guid organizationId, CalculateTaxRequest request, CancellationToken ct)
    {
        identity.Validate(); if (organizationId == Guid.Empty || request.ProfileId == Guid.Empty || request.BranchId == Guid.Empty || request.Amount < 0 || decimal.Round(request.Amount, 6) != request.Amount || request.At == default || request.At.Offset != TimeSpan.Zero) throw new ArgumentException("Tax calculation is invalid.");
        var category = request.CategoryCode?.Trim().ToUpperInvariant() ?? ""; if (category.Length is < 1 or > 40) throw new ArgumentException("Tax category is invalid.");
        var data = source ?? throw new TaxUnavailableException(); await using var c = await data.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, organizationId, identity, ct); await Demand(c, t, organizationId, request.BranchId, identity, "taxation.view", ct);
        var profile = await ReadProfileById(c, t, organizationId, request.ProfileId, ct) ?? throw new TaxNotFoundException();
        await using var q = new NpgsqlCommand("""
            SELECT rate_id,profile_id,branch_id,category_code,rate_percent,effective_from,effective_until,is_active,row_version
            FROM taxation.tax_rates WHERE organization_id=$1 AND profile_id=$2 AND category_code=$3 AND is_active
              AND (branch_id IS NULL OR branch_id=$4) AND effective_from<=$5 AND (effective_until IS NULL OR effective_until>$5)
            ORDER BY branch_id IS NOT NULL DESC,effective_from DESC,rate_id LIMIT 1
            """, c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(request.ProfileId); q.Parameters.AddWithValue(category); q.Parameters.AddWithValue(request.BranchId); q.Parameters.AddWithValue(request.At);
        RateRow? rate = null; await using (var r = await q.ExecuteReaderAsync(ct)) if (await r.ReadAsync(ct)) rate = ReadRate(r);
        var percent = rate?.Rate ?? 0m; decimal net, tax, gross;
        if (profile.Inclusive) { gross = request.Amount; net = percent == 0 ? gross : decimal.Round(gross / (1m + percent / 100m), 6, MidpointRounding.AwayFromZero); tax = gross - net; }
        else { net = request.Amount; tax = decimal.Round(net * percent / 100m, 6, MidpointRounding.AwayFromZero); gross = net + tax; }
        await t.CommitAsync(ct); return new(profile.Id, rate?.Id, category, percent, net, tax, gross, profile.Inclusive);
    }
    private static async Task<ProfileRow?> ReadProfileByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid operationId, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("SELECT profile_id,code,name,country_code,prices_include_tax,is_active,row_version,created_at,updated_at FROM taxation.tax_profiles WHERE organization_id=$1 AND operation_id=$2", c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(operationId); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadProfile(r) : null;
    }
    private static async Task<ProfileRow?> ReadProfileById(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid profileId, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("SELECT profile_id,code,name,country_code,prices_include_tax,is_active,row_version,created_at,updated_at FROM taxation.tax_profiles WHERE organization_id=$1 AND profile_id=$2 AND is_active", c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(profileId); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadProfile(r) : null;
    }
    private static async Task<RateRow?> ReadRateByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid operationId, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("SELECT rate_id,profile_id,branch_id,category_code,rate_percent,effective_from,effective_until,is_active,row_version FROM taxation.tax_rates WHERE organization_id=$1 AND operation_id=$2", c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(operationId); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadRate(r) : null;
    }
    private static async Task EnsureProfile(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid profileId, CancellationToken ct)
    {
        if (await ReadProfileById(c, t, organizationId, profileId, ct) is null) throw new TaxNotFoundException();
    }
    private static async Task EnsureBranch(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid branchId, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT FROM organization.branches WHERE organization_id=$1 AND branch_id=$2 AND is_active)", c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(branchId); if (await q.ExecuteScalarAsync(ct) is not true) throw new TaxConflictException();
    }
    private static ProfileRow ReadProfile(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4), r.GetBoolean(5), r.GetInt64(6), r.GetFieldValue<DateTimeOffset>(7), r.GetFieldValue<DateTimeOffset>(8));
    private static RateRow ReadRate(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetDecimal(4), r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6), r.GetBoolean(7), r.GetInt64(8));
    private static TaxProfileResponse ToProfileResponse(ProfileRow r) => new(r.Id, r.Code, r.Name, r.CountryCode, r.Inclusive, r.Active, r.Version, r.CreatedAt, r.UpdatedAt);
    private static TaxRateResponse ToRateResponse(RateRow r) => new(r.Id, r.ProfileId, r.BranchId, r.Category, r.Rate, r.From, r.Until, r.Active, r.Version);
    private static async Task Prepare(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, TaxIdentity identity, CancellationToken ct)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime' AND EXISTS(SELECT FROM pg_class x JOIN pg_namespace n ON n.oid=x.relnamespace
              WHERE n.nspname='taxation' AND x.relname='tax_profiles' AND x.relrowsecurity AND x.relforcerowsecurity
                AND x.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, c, t);
        if (await safety.ExecuteScalarAsync(ct) is not true) throw new TaxUnavailableException();
        await using var context = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t);
        context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer); context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(ct);
    }
    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid? branchId, TaxIdentity identity, string permission, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active AND m.valid_from<=statement_timestamp()
                AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$4
                AND (g.scope_kind='organization' OR ($5::uuid IS NOT NULL AND g.scope_kind='branch' AND g.branch_id=$5)))
            """, c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject); q.Parameters.AddWithValue(permission);
        q.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = branchId.HasValue ? branchId.Value : DBNull.Value }); if (await q.ExecuteScalarAsync(ct) is not true) throw new TaxDeniedException();
    }
}
