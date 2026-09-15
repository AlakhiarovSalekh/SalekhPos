using System.Data;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Loyalty.Application.Accounts;
using SalekhPos.Loyalty.Contracts.Accounts;
using SalekhPos.Loyalty.Domain.Accounts;

namespace SalekhPos.Loyalty.Infrastructure.Accounts;

public sealed class PostgresLoyaltyAccountService(NpgsqlDataSource? source) : ILoyaltyAccountService
{
    private sealed record AccountRow(Guid Id, Guid CustomerId, string Tier, long Balance, long Lifetime,
        long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    public async Task<(LoyaltyAccountResponse Account, bool Created)> OpenAsync(LoyaltyIdentity identity,
        OpenLoyaltyAccountCommand command, CancellationToken ct)
    {
        identity.Validate();
        if (command.OrganizationId == Guid.Empty || command.AccountId == Guid.Empty
            || command.OperationId == Guid.Empty || command.CustomerId == Guid.Empty)
            throw new ArgumentException("Loyalty account request is invalid.");
        var ds = source ?? throw new LoyaltyUnavailableException();
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var t = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await Prepare(c, t, command.OrganizationId, identity, ct);
        await Demand(c, t, command.OrganizationId, identity, "loyalty.manage", ct);
        await using (var customer = new NpgsqlCommand("SELECT EXISTS(SELECT FROM customers.customers WHERE organization_id=$1 AND customer_id=$2 AND is_active)", c, t))
        {
            customer.Parameters.AddWithValue(command.OrganizationId); customer.Parameters.AddWithValue(command.CustomerId);
            if (await customer.ExecuteScalarAsync(ct) is not true) throw new LoyaltyConflictException();
        }
        var created = await InsertAccount(c, t, command, identity, ct);
        if (created is not null) { await t.CommitAsync(ct); return (ToResponse(created), true); }
        var replay = await ReadByOperation(c, t, command.OrganizationId, command.OperationId, ct)
            ?? throw new LoyaltyUnavailableException();
        if (replay.CustomerId != command.CustomerId || replay.Id != command.AccountId)
            throw new LoyaltyConflictException();
        await t.CommitAsync(ct); return (ToResponse(replay), false);
    }

    public async Task<LoyaltyAccountPage> ListAsync(LoyaltyIdentity identity, Guid organizationId,
        int pageSize, Guid? after, CancellationToken ct)
    {
        ValidateQuery(identity, organizationId, pageSize, after);
        var ds = source ?? throw new LoyaltyUnavailableException();
        await using var c = await ds.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Prepare(c, t, organizationId, identity, ct); await Demand(c, t, organizationId, identity, "loyalty.view", ct);
        await using var q = new NpgsqlCommand($"{AccountSelect} WHERE organization_id=$1 AND ($2::uuid IS NULL OR account_id>$2) ORDER BY account_id LIMIT $3", c, t);
        q.Parameters.AddWithValue(organizationId); q.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Uuid, Value=after.HasValue?after.Value:DBNull.Value }); q.Parameters.AddWithValue(pageSize+1);
        var rows = new List<AccountRow>(); await using (var r = await q.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) rows.Add(ReadAccount(r));
        Guid? next=null; if(rows.Count>pageSize){ rows.RemoveAt(pageSize); next=rows[^1].Id; }
        await t.CommitAsync(ct); return new(rows.Select(ToResponse).ToArray(), next);
    }

    public async Task<LoyaltyAccountResponse?> ReadAsync(LoyaltyIdentity identity, Guid organizationId,
        Guid accountId, CancellationToken ct)
    {
        identity.Validate(); if(organizationId==Guid.Empty||accountId==Guid.Empty) throw new ArgumentException("Loyalty account query is invalid.");
        var ds=source??throw new LoyaltyUnavailableException(); await using var c=await ds.OpenConnectionAsync(ct); await using var t=await c.BeginTransactionAsync(ct);
        await Prepare(c,t,organizationId,identity,ct); await Demand(c,t,organizationId,identity,"loyalty.view",ct);
        var row=await ReadAccount(c,t,organizationId,accountId,false,ct); await t.CommitAsync(ct); return row is null?null:ToResponse(row);
    }

    public Task<LoyaltyPointsResult> EarnAsync(LoyaltyIdentity identity, ChangeLoyaltyPointsCommand command, CancellationToken ct) =>
        ChangePointsAsync(identity, command, false, ct);
    public Task<LoyaltyPointsResult> RedeemAsync(LoyaltyIdentity identity, ChangeLoyaltyPointsCommand command, CancellationToken ct) =>
        ChangePointsAsync(identity, command, true, ct);

    private async Task<LoyaltyPointsResult> ChangePointsAsync(LoyaltyIdentity identity, ChangeLoyaltyPointsCommand command,
        bool redeem, CancellationToken ct)
    {
        identity.Validate(); LoyaltyAccount.ValidatePoints(command.Points);
        if(command.OrganizationId==Guid.Empty||command.AccountId==Guid.Empty||command.OperationId==Guid.Empty||InvalidReason(command.Reason))
            throw new ArgumentException("Loyalty points request is invalid.");
        var ds=source??throw new LoyaltyUnavailableException(); await using var c=await ds.OpenConnectionAsync(ct); await using var t=await c.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        await Prepare(c,t,command.OrganizationId,identity,ct); await Demand(c,t,command.OrganizationId,identity,"loyalty.manage",ct);
        await Lock(c,t,$"loyalty:{command.OrganizationId:D}:{command.AccountId:D}",ct);
        var replay=await ReadEventByOperation(c,t,command.OrganizationId,command.OperationId,ct);
        if(replay is not null)
        {
            if(replay.Value.AccountId!=command.AccountId || replay.Value.Kind!=(redeem?"redeem":"earn") || Math.Abs(replay.Value.Delta)!=command.Points)
                throw new LoyaltyConflictException();
            var baseAccount=await ReadAccount(c,t,command.OrganizationId,command.AccountId,false,ct)??throw new LoyaltyNotFoundException();
            var snap=new LoyaltyAccountResponse(baseAccount.Id,baseAccount.CustomerId,replay.Value.Tier,replay.Value.Balance,
                replay.Value.Lifetime,replay.Value.Version,baseAccount.CreatedAt,replay.Value.OccurredAt);
            await t.CommitAsync(ct); return new(snap,replay.Value.EventId,replay.Value.Kind,Math.Abs(replay.Value.Delta),replay.Value.OccurredAt,false);
        }
        var current=await ReadAccount(c,t,command.OrganizationId,command.AccountId,true,ct)??throw new LoyaltyNotFoundException();
        var delta=redeem?-command.Points:command.Points;
        if(redeem && current.Balance<command.Points) throw new InsufficientLoyaltyPointsException();
        var balance=current.Balance+delta; var lifetime=current.Lifetime+(redeem?0:command.Points);
        var tier=LoyaltyAccount.TierFor(lifetime).ToString().ToLowerInvariant(); var version=current.Version+1;
        var occurred=await DatabaseTime(c,t,ct); var eventId=Guid.NewGuid();
        await UpdateAccount(c,t,command.OrganizationId,command.AccountId,balance,lifetime,tier,version,occurred,ct);
        await InsertEvent(c,t,command,eventId,redeem?"redeem":"earn",delta,balance,lifetime,tier,version,occurred,identity,ct);
        await t.CommitAsync(ct);
        var result=new LoyaltyAccountResponse(current.Id,current.CustomerId,tier,balance,lifetime,version,current.CreatedAt,occurred);
        return new(result,eventId,redeem?"redeem":"earn",command.Points,occurred,true);
    }
    public async Task<LoyaltyEventPage> ListEventsAsync(LoyaltyIdentity identity, Guid organizationId,
        Guid accountId, int pageSize, Guid? after, CancellationToken ct)
    {
        ValidateQuery(identity,organizationId,pageSize,after); if(accountId==Guid.Empty) throw new ArgumentException("Loyalty account is required.");
        var ds=source??throw new LoyaltyUnavailableException(); await using var c=await ds.OpenConnectionAsync(ct); await using var t=await c.BeginTransactionAsync(ct);
        await Prepare(c,t,organizationId,identity,ct); await Demand(c,t,organizationId,identity,"loyalty.view",ct);
        if(await ReadAccount(c,t,organizationId,accountId,false,ct) is null) throw new LoyaltyNotFoundException();
        await using var q=new NpgsqlCommand("SELECT event_id,kind,points_delta,reason,occurred_at FROM loyalty.point_events WHERE organization_id=$1 AND account_id=$2 AND ($3::uuid IS NULL OR event_id>$3) ORDER BY event_id LIMIT $4",c,t);
        q.Parameters.AddWithValue(organizationId);q.Parameters.AddWithValue(accountId);q.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Uuid,Value=after.HasValue?after.Value:DBNull.Value});q.Parameters.AddWithValue(pageSize+1);
        var rows=new List<LoyaltyEventResponse>(); await using(var r=await q.ExecuteReaderAsync(ct)) while(await r.ReadAsync(ct)) rows.Add(new(r.GetGuid(0),r.GetString(1),r.GetInt32(2),r.GetString(3),r.GetFieldValue<DateTimeOffset>(4)));
        Guid? next=null;if(rows.Count>pageSize){rows.RemoveAt(pageSize);next=rows[^1].Id;}await t.CommitAsync(ct);return new(rows.AsReadOnly(),next);
    }

    private static async Task<AccountRow?> InsertAccount(NpgsqlConnection c,NpgsqlTransaction t,OpenLoyaltyAccountCommand x,LoyaltyIdentity identity,CancellationToken ct)
    {
        await using var q=new NpgsqlCommand("INSERT INTO loyalty.accounts(organization_id,account_id,operation_id,customer_id,tier,issuer,subject) VALUES($1,$2,$3,$4,'bronze',$5,$6) ON CONFLICT(organization_id,operation_id) DO NOTHING RETURNING account_id,customer_id,tier,points_balance,lifetime_points,row_version,created_at,updated_at",c,t);
        q.Parameters.AddWithValue(x.OrganizationId);q.Parameters.AddWithValue(x.AccountId);q.Parameters.AddWithValue(x.OperationId);q.Parameters.AddWithValue(x.CustomerId);q.Parameters.AddWithValue(identity.Issuer);q.Parameters.AddWithValue(identity.Subject);
        await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?ReadAccount(r):null;
    }
    private static async Task<AccountRow?> ReadByOperation(NpgsqlConnection c,NpgsqlTransaction t,Guid org,Guid op,CancellationToken ct)
    { await using var q=new NpgsqlCommand($"{AccountSelect} WHERE organization_id=$1 AND operation_id=$2",c,t);q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(op);await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?ReadAccount(r):null; }

    private static async Task<AccountRow?> ReadAccount(NpgsqlConnection c,NpgsqlTransaction t,Guid org,Guid account,bool forUpdate,CancellationToken ct)
    { await using var q=new NpgsqlCommand($"{AccountSelect} WHERE organization_id=$1 AND account_id=$2{(forUpdate?" FOR UPDATE":"")}",c,t);q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(account);await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?ReadAccount(r):null; }

    private static async Task<(Guid EventId,Guid AccountId,string Kind,int Delta,long Balance,long Lifetime,string Tier,long Version,DateTimeOffset OccurredAt)?> ReadEventByOperation(NpgsqlConnection c,NpgsqlTransaction t,Guid org,Guid op,CancellationToken ct)
    { await using var q=new NpgsqlCommand("SELECT event_id,account_id,kind,points_delta,balance_after,lifetime_points_after,tier_after,version_after,occurred_at FROM loyalty.point_events WHERE organization_id=$1 AND operation_id=$2",c,t);q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(op);await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;return(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetInt32(3),r.GetInt64(4),r.GetInt64(5),r.GetString(6),r.GetInt64(7),r.GetFieldValue<DateTimeOffset>(8)); }

    private static async Task UpdateAccount(NpgsqlConnection c,NpgsqlTransaction t,Guid org,Guid account,long balance,long lifetime,string tier,long version,DateTimeOffset at,CancellationToken ct)
    { await using var q=new NpgsqlCommand("UPDATE loyalty.accounts SET points_balance=$3,lifetime_points=$4,tier=$5,row_version=$6,updated_at=$7 WHERE organization_id=$1 AND account_id=$2",c,t);q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(account);q.Parameters.AddWithValue(balance);q.Parameters.AddWithValue(lifetime);q.Parameters.AddWithValue(tier);q.Parameters.AddWithValue(version);q.Parameters.AddWithValue(at);if(await q.ExecuteNonQueryAsync(ct)!=1)throw new LoyaltyUnavailableException(); }
    private static async Task InsertEvent(NpgsqlConnection c,NpgsqlTransaction t,ChangeLoyaltyPointsCommand x,Guid eventId,string kind,int delta,long balance,long lifetime,string tier,long version,DateTimeOffset at,LoyaltyIdentity identity,CancellationToken ct)
    { await using var q=new NpgsqlCommand("INSERT INTO loyalty.point_events(organization_id,event_id,operation_id,account_id,kind,points_delta,balance_after,lifetime_points_after,tier_after,version_after,reason,occurred_at,issuer,subject) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)",c,t);q.Parameters.AddWithValue(x.OrganizationId);q.Parameters.AddWithValue(eventId);q.Parameters.AddWithValue(x.OperationId);q.Parameters.AddWithValue(x.AccountId);q.Parameters.AddWithValue(kind);q.Parameters.AddWithValue(delta);q.Parameters.AddWithValue(balance);q.Parameters.AddWithValue(lifetime);q.Parameters.AddWithValue(tier);q.Parameters.AddWithValue(version);q.Parameters.AddWithValue(x.Reason.Trim());q.Parameters.AddWithValue(at);q.Parameters.AddWithValue(identity.Issuer);q.Parameters.AddWithValue(identity.Subject);await q.ExecuteNonQueryAsync(ct); }

    private static async Task Prepare(NpgsqlConnection c,NpgsqlTransaction t,Guid org,LoyaltyIdentity identity,CancellationToken ct)
    { await using(var safety=new NpgsqlCommand("SELECT current_user='salekhpos_runtime' AND EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='loyalty' AND c.relname='accounts' AND c.relrowsecurity AND c.relforcerowsecurity AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))",c,t)) if(await safety.ExecuteScalarAsync(ct) is not true)throw new LoyaltyUnavailableException(); await using var q=new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",c,t);q.Parameters.AddWithValue(org.ToString());q.Parameters.AddWithValue(identity.Issuer);q.Parameters.AddWithValue(identity.Subject);await q.ExecuteNonQueryAsync(ct); }

    private static async Task Demand(NpgsqlConnection c,NpgsqlTransaction t,Guid org,LoyaltyIdentity identity,string permission,CancellationToken ct)
    { await using var q=new NpgsqlCommand("SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id JOIN organization.organizations o ON o.organization_id=m.organization_id WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active AND o.is_active AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$4)",c,t);q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(identity.Issuer);q.Parameters.AddWithValue(identity.Subject);q.Parameters.AddWithValue(permission);if(await q.ExecuteScalarAsync(ct) is not true)throw new LoyaltyDeniedException(); }
    private static async Task Lock(NpgsqlConnection c,NpgsqlTransaction t,string key,CancellationToken ct)
    { await using var q=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))",c,t);q.Parameters.AddWithValue(key);await q.ExecuteNonQueryAsync(ct); }
    private static async Task<DateTimeOffset> DatabaseTime(NpgsqlConnection c,NpgsqlTransaction t,CancellationToken ct)
    { await using var q=new NpgsqlCommand("SELECT statement_timestamp()",c,t);var v=await q.ExecuteScalarAsync(ct);return v is DateTimeOffset x?x:throw new LoyaltyUnavailableException(); }
    private static AccountRow ReadAccount(NpgsqlDataReader r)=>new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetInt64(3),r.GetInt64(4),r.GetInt64(5),r.GetFieldValue<DateTimeOffset>(6),r.GetFieldValue<DateTimeOffset>(7));
    private static LoyaltyAccountResponse ToResponse(AccountRow x)=>new(x.Id,x.CustomerId,x.Tier,x.Balance,x.Lifetime,x.Version,x.CreatedAt,x.UpdatedAt);
    private static bool InvalidReason(string? value)=>string.IsNullOrWhiteSpace(value)||value!=value.Trim()||value.Length>200||value.Any(char.IsControl);
    private static void ValidateQuery(LoyaltyIdentity identity,Guid org,int pageSize,Guid? after)
    { identity.Validate();if(org==Guid.Empty||pageSize is <1 or >100||after==Guid.Empty)throw new ArgumentException("Loyalty query is invalid."); }
    private const string AccountSelect="SELECT account_id,customer_id,tier,points_balance,lifetime_points,row_version,created_at,updated_at FROM loyalty.accounts";
}
