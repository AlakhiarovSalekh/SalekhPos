using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Subscriptions.Application.Subscriptions;
using SalekhPos.Subscriptions.Contracts.Subscriptions;
using SalekhPos.Subscriptions.Domain.Entitlements;
using SalekhPos.Subscriptions.Domain.Plans;
using SalekhPos.Subscriptions.Domain.Subscriptions;

namespace SalekhPos.Subscriptions.Infrastructure.Subscriptions;

public sealed class PostgresSubscriptionService(NpgsqlDataSource? source) : ISubscriptionService
{
    public async Task<IReadOnlyList<PlanResponse>> ListPlansAsync(SubscriptionIdentity identity,
        Guid organizationId, CancellationToken cancellationToken)
    {
        ValidateIdentity(identity, organizationId);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, identity, organizationId, cancellationToken);
        await Demand(connection, transaction, identity, organizationId, "subscriptions.view", cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT plan_id,code,name,price,currency,billing_interval,entitlements,is_active
            FROM subscriptions.plans WHERE is_active ORDER BY price,code LIMIT 100
            """, connection, transaction);
        var values = new List<PlanResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) values.Add(ReadPlan(reader));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return values;
    }

    public async Task<SubscriptionResponse> CreateAsync(SubscriptionIdentity identity, Guid organizationId,
        CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        ValidateIdentity(identity, organizationId);
        if (request.OperationId == Guid.Empty || request.SubscriptionId == Guid.Empty || request.PlanId == Guid.Empty)
            throw new ArgumentException("Subscription identifiers are invalid.");
        var model = new Subscription(request.SubscriptionId, organizationId, request.PlanId,
            request.PeriodStart, request.PeriodEnd, request.Trial);
        var hash = Hash(request);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, identity, organizationId, cancellationToken);
        await Demand(connection, transaction, identity, organizationId, "subscriptions.manage", cancellationToken);
        var plan = await ReadPlan(connection, transaction, request.PlanId, cancellationToken)
            ?? throw new SubscriptionNotFoundException();
        if (!plan.Active) throw new SubscriptionConflictException("The plan is inactive.");
        await using var insert = new NpgsqlCommand("""
            INSERT INTO subscriptions.subscriptions(
              organization_id,subscription_id,plan_id,operation_id,request_hash,status,
              period_start,period_end,cancel_at_period_end,canceled_at,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,false,NULL,$9,$10)
            ON CONFLICT(organization_id,operation_id) DO NOTHING
            RETURNING subscription_id,plan_id,status,period_start,period_end,cancel_at_period_end,canceled_at,row_version
            """, connection, transaction);
        insert.Parameters.AddWithValue(organizationId);
        insert.Parameters.AddWithValue(model.Id);
        insert.Parameters.AddWithValue(model.PlanId);
        insert.Parameters.AddWithValue(request.OperationId);
        insert.Parameters.AddWithValue(hash);
        insert.Parameters.AddWithValue(Snake(model.Status));
        insert.Parameters.AddWithValue(model.PeriodStart);
        insert.Parameters.AddWithValue(model.PeriodEnd);
        insert.Parameters.AddWithValue(identity.Issuer);
        insert.Parameters.AddWithValue(identity.Subject);
        SubRow? row;
        try
        {
            row = await ReadSubscriptionRow(insert, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new SubscriptionConflictException("An active subscription already exists.");
        }
        if (row is null)
        {
            var replay = await ReadByOperation(connection, transaction, organizationId, request.OperationId, cancellationToken);
            if (replay.Row is null || replay.Hash != hash) throw new SubscriptionConflictException("Operation payload differs.");
            await transaction.CommitAsync(cancellationToken);
            return replay.Row.Value.Response;
        }
        await WriteEntitlements(connection, transaction, organizationId, row.Value.Response.SubscriptionId,
            row.Value.Version, plan.Entitlements, row.Value.Response.PeriodEnd, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return row.Value.Response;
    }

    public async Task<SubscriptionResponse> GetAsync(SubscriptionIdentity identity, Guid organizationId,
        Guid subscriptionId, CancellationToken cancellationToken)
    {
        ValidateIdentity(identity, organizationId); Require(subscriptionId, nameof(subscriptionId));
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, identity, organizationId, cancellationToken);
        await Demand(connection, transaction, identity, organizationId, "subscriptions.view", cancellationToken);
        var row = await ReadCurrent(connection, transaction, organizationId, subscriptionId, false, cancellationToken)
            ?? throw new SubscriptionNotFoundException();
        await transaction.CommitAsync(cancellationToken);
        return row.Response;
    }

    public async Task<SubscriptionResponse> ChangePlanAsync(SubscriptionIdentity identity, Guid organizationId,
        Guid subscriptionId, ChangePlanRequest request, CancellationToken cancellationToken)
    {
        ValidateMutation(identity, organizationId, subscriptionId, request.OperationId);
        Require(request.PlanId, nameof(request.PlanId));
        var hash = Hash(request);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, identity, organizationId, cancellationToken);
        await Demand(connection, transaction, identity, organizationId, "subscriptions.manage", cancellationToken);
        var replay = await Replay(connection, transaction, organizationId, request.OperationId, "change_plan", hash, cancellationToken);
        if (replay is not null) { await transaction.CommitAsync(cancellationToken); return replay; }
        var current = await ReadCurrent(connection, transaction, organizationId, subscriptionId, true, cancellationToken)
            ?? throw new SubscriptionNotFoundException();
        var plan = await ReadPlan(connection, transaction, request.PlanId, cancellationToken)
            ?? throw new SubscriptionNotFoundException();
        if (!plan.Active) throw new SubscriptionConflictException("The plan is inactive.");
        var model = Model(organizationId, current.Response); model.ChangePlan(request.PlanId);
        var version = await Update(connection, transaction, organizationId, model, current.Version, cancellationToken);
        await RecordOperation(connection, transaction, identity, organizationId, subscriptionId, request.OperationId,
            hash, "change_plan", cancellationToken);
        await WriteEntitlements(connection, transaction, organizationId, subscriptionId, version,
            plan.Entitlements, model.PeriodEnd, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Response(model);
    }

    public async Task<SubscriptionResponse> CancelAsync(SubscriptionIdentity identity, Guid organizationId,
        Guid subscriptionId, CancelSubscriptionRequest request, CancellationToken cancellationToken)
    {
        ValidateMutation(identity, organizationId, subscriptionId, request.OperationId);
        if (request.EffectiveAt.HasValue && request.EffectiveAt.Value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Cancellation time must be UTC.");
        var hash = Hash(request);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, identity, organizationId, cancellationToken);
        await Demand(connection, transaction, identity, organizationId, "subscriptions.manage", cancellationToken);
        var replay = await Replay(connection, transaction, organizationId, request.OperationId, "cancel", hash, cancellationToken);
        if (replay is not null) { await transaction.CommitAsync(cancellationToken); return replay; }
        var current = await ReadCurrent(connection, transaction, organizationId, subscriptionId, true, cancellationToken)
            ?? throw new SubscriptionNotFoundException();
        var model = Model(organizationId, current.Response);
        if (request.Immediately) model.CancelImmediately(request.EffectiveAt ?? DateTimeOffset.UtcNow);
        else model.ScheduleCancellation();
        await Update(connection, transaction, organizationId, model, current.Version, cancellationToken);
        await RecordOperation(connection, transaction, identity, organizationId, subscriptionId, request.OperationId,
            hash, "cancel", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Response(model);
    }

    public async Task<SubscriptionResponse> RenewAsync(SubscriptionIdentity identity, Guid organizationId,
        Guid subscriptionId, RenewSubscriptionRequest request, CancellationToken cancellationToken)
    {
        ValidateMutation(identity, organizationId, subscriptionId, request.OperationId);
        var hash = Hash(request);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, identity, organizationId, cancellationToken);
        await Demand(connection, transaction, identity, organizationId, "subscriptions.manage", cancellationToken);
        var replay = await Replay(connection, transaction, organizationId, request.OperationId, "renew", hash, cancellationToken);
        if (replay is not null) { await transaction.CommitAsync(cancellationToken); return replay; }
        var current = await ReadCurrent(connection, transaction, organizationId, subscriptionId, true, cancellationToken)
            ?? throw new SubscriptionNotFoundException();
        var model = Model(organizationId, current.Response);
        model.Renew(request.PeriodStart, request.PeriodEnd);
        var version = await Update(connection, transaction, organizationId, model, current.Version, cancellationToken);
        await RecordOperation(connection, transaction, identity, organizationId, subscriptionId, request.OperationId,
            hash, "renew", cancellationToken);
        var plan = await ReadPlan(connection, transaction, model.PlanId, cancellationToken)
            ?? throw new SubscriptionUnavailableException();
        await WriteEntitlements(connection, transaction, organizationId, subscriptionId, version,
            plan.Entitlements, model.PeriodEnd, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Response(model);
    }

    public async Task<EntitlementResponse> GetEntitlementsAsync(SubscriptionIdentity identity, Guid organizationId,
        Guid subscriptionId, CancellationToken cancellationToken)
    {
        ValidateIdentity(identity, organizationId); Require(subscriptionId, nameof(subscriptionId));
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, identity, organizationId, cancellationToken);
        await Demand(connection, transaction, identity, organizationId, "subscriptions.view", cancellationToken);
        var current = await ReadCurrent(connection, transaction, organizationId, subscriptionId, false, cancellationToken)
            ?? throw new SubscriptionNotFoundException();
        await using var command = new NpgsqlCommand("""
            SELECT limits,valid_until FROM subscriptions.entitlement_snapshots
            WHERE organization_id=$1 AND subscription_id=$2 AND row_version=$3
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.AddWithValue(subscriptionId);
        command.Parameters.AddWithValue(current.Version);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new SubscriptionUnavailableException();
        var limits = ParseLimits(reader.GetString(0));
        var validUntil = reader.GetFieldValue<DateTimeOffset>(1);
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(subscriptionId, limits, validUntil);
    }

    private async Task<NpgsqlConnection> Open(CancellationToken ct) =>
        await (source ?? throw new SubscriptionUnavailableException()).OpenConnectionAsync(ct);

    private static async Task Prepare(NpgsqlConnection c, NpgsqlTransaction t, SubscriptionIdentity i,
        Guid organizationId, CancellationToken ct)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime' AND EXISTS(
              SELECT FROM pg_class x JOIN pg_namespace n ON n.oid=x.relnamespace
              WHERE n.nspname='subscriptions' AND x.relname='subscriptions'
                AND x.relrowsecurity AND x.relforcerowsecurity
                AND x.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, c, t);
        if (await safety.ExecuteScalarAsync(ct) is not true) throw new SubscriptionUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t);
        context.Parameters.AddWithValue(organizationId.ToString());
        context.Parameters.AddWithValue(i.Issuer);
        context.Parameters.AddWithValue(i.Subject);
        await context.ExecuteNonQueryAsync(ct);
    }

    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, SubscriptionIdentity i,
        Guid organizationId, string permission, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g
              ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active
                AND m.valid_from<=statement_timestamp()
                AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission=$4 AND g.scope_kind='organization')
            """, c, t);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(i.Issuer);
        command.Parameters.AddWithValue(i.Subject); command.Parameters.AddWithValue(permission);
        if (await command.ExecuteScalarAsync(ct) is not true) throw new SubscriptionDeniedException();
    }

    private static async Task<PlanData?> ReadPlan(NpgsqlConnection c, NpgsqlTransaction t, Guid id, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("""
            SELECT plan_id,code,name,price,currency,billing_interval,entitlements,is_active
            FROM subscriptions.plans WHERE plan_id=$1
            """, c, t);
        q.Parameters.AddWithValue(id); await using var r=await q.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Plan(r) : null;
    }
    private static PlanData Plan(NpgsqlDataReader r)
    {
        var interval = r.GetString(5) switch { "monthly"=>BillingInterval.Monthly, "annual"=>BillingInterval.Annual, _=>throw new SubscriptionUnavailableException() };
        var model = new SubscriptionPlan(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.GetString(4),
            interval, ParseLimits(r.GetString(6)), r.GetBoolean(7));
        return new(model.Id, model.Code, model.Name, model.Price, model.Currency,
            model.Interval==BillingInterval.Monthly?"monthly":"annual", model.Entitlements, model.Active);
    }
    private static PlanResponse ReadPlan(NpgsqlDataReader r)
    {
        var p=Plan(r); return new(p.Id,p.Code,p.Name,p.Price,p.Currency,p.Interval,p.Entitlements,p.Active);
    }

    private static async Task<SubRow?> ReadCurrent(NpgsqlConnection c,NpgsqlTransaction t,Guid org,Guid id,bool locked,CancellationToken ct)
    {
        await using var q=new NpgsqlCommand($"""
            SELECT subscription_id,plan_id,status,period_start,period_end,cancel_at_period_end,canceled_at,row_version
            FROM subscriptions.subscriptions WHERE organization_id=$1 AND subscription_id=$2{(locked?" FOR UPDATE":"")}
            """,c,t);
        q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(id);return await ReadSubscriptionRow(q,ct);
    }
    private static async Task<(SubRow? Row,string? Hash)> ReadByOperation(NpgsqlConnection c,NpgsqlTransaction t,Guid org,Guid op,CancellationToken ct)
    {
        await using var q=new NpgsqlCommand("""
            SELECT subscription_id,plan_id,status,period_start,period_end,cancel_at_period_end,canceled_at,row_version,request_hash
            FROM subscriptions.subscriptions WHERE organization_id=$1 AND operation_id=$2
            """,c,t);
        q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(op);await using var r=await q.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))return(null,null);return(Row(r),r.GetString(8));
    }
    private static async Task<SubRow?> ReadSubscriptionRow(NpgsqlCommand q,CancellationToken ct)
    { await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?Row(r):null; }
    private static SubRow Row(NpgsqlDataReader r)=>new(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),
        r.GetFieldValue<DateTimeOffset>(3),r.GetFieldValue<DateTimeOffset>(4),r.GetBoolean(5),
        r.IsDBNull(6)?null:r.GetFieldValue<DateTimeOffset>(6)),r.GetInt64(7));

    private static async Task<SubscriptionResponse?> Replay(NpgsqlConnection c,NpgsqlTransaction t,Guid org,
        Guid operationId,string type,string hash,CancellationToken ct)
    {
        await using var q=new NpgsqlCommand("""
            SELECT subscription_id,request_hash,operation_type FROM subscriptions.operations
            WHERE organization_id=$1 AND operation_id=$2
            """,c,t);
        q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(operationId);await using var r=await q.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))return null;
        var id=r.GetGuid(0);if(r.GetString(1)!=hash||r.GetString(2)!=type)throw new SubscriptionConflictException("Operation payload differs.");
        await r.DisposeAsync();return (await ReadCurrent(c,t,org,id,false,ct))?.Response??throw new SubscriptionUnavailableException();
    }

    private static async Task<long> Update(NpgsqlConnection c,NpgsqlTransaction t,Guid org,Subscription model,long expected,CancellationToken ct)
    {
        await using var q=new NpgsqlCommand("""
            UPDATE subscriptions.subscriptions SET plan_id=$3,status=$4,period_start=$5,period_end=$6,
              cancel_at_period_end=$7,canceled_at=$8,row_version=row_version+1
            WHERE organization_id=$1 AND subscription_id=$2 AND row_version=$9
            RETURNING row_version
            """,c,t);
        q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(model.Id);q.Parameters.AddWithValue(model.PlanId);
        q.Parameters.AddWithValue(Snake(model.Status));q.Parameters.AddWithValue(model.PeriodStart);q.Parameters.AddWithValue(model.PeriodEnd);
        q.Parameters.AddWithValue(model.CancelAtPeriodEnd);q.Parameters.AddWithValue((object?)model.CanceledAt??DBNull.Value);q.Parameters.AddWithValue(expected);
        var result=await q.ExecuteScalarAsync(ct);return result is long v?v:throw new SubscriptionConflictException("Subscription changed concurrently.");
    }
    private static async Task RecordOperation(NpgsqlConnection c,NpgsqlTransaction t,SubscriptionIdentity i,Guid org,
        Guid id,Guid operationId,string hash,string type,CancellationToken ct)
    {
        await using var q=new NpgsqlCommand("""
            INSERT INTO subscriptions.operations(organization_id,subscription_id,operation_id,request_hash,operation_type,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7)
            """,c,t);
        q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(id);q.Parameters.AddWithValue(operationId);q.Parameters.AddWithValue(hash);
        q.Parameters.AddWithValue(type);q.Parameters.AddWithValue(i.Issuer);q.Parameters.AddWithValue(i.Subject);await q.ExecuteNonQueryAsync(ct);
    }
    private static async Task WriteEntitlements(NpgsqlConnection c,NpgsqlTransaction t,Guid org,Guid id,long version,
        IReadOnlyDictionary<string,long> limits,DateTimeOffset validUntil,CancellationToken ct)
    {
        _=new EntitlementSnapshot(id,limits,validUntil);
        await using var q=new NpgsqlCommand("""
            INSERT INTO subscriptions.entitlement_snapshots(organization_id,subscription_id,row_version,limits,valid_until)
            VALUES($1,$2,$3,$4,$5)
            """,c,t);
        q.Parameters.AddWithValue(org);q.Parameters.AddWithValue(id);q.Parameters.AddWithValue(version);
        q.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Jsonb,Value=JsonSerializer.Serialize(limits)});
        q.Parameters.AddWithValue(validUntil);await q.ExecuteNonQueryAsync(ct);
    }

    private static Subscription Model(Guid org,SubscriptionResponse r)
    {
        var m=new Subscription(r.SubscriptionId,org,r.PlanId,r.PeriodStart,r.PeriodEnd,r.Status=="trialing");
        if(r.Status=="past_due")m.MarkPastDue();
        if(r.CancelAtPeriodEnd)m.ScheduleCancellation();
        if(r.Status=="canceled")m.CancelImmediately(r.CanceledAt??r.PeriodStart);
        if(r.Status=="expired")throw new SubscriptionConflictException("Expired subscriptions are final.");
        return m;
    }
    private static SubscriptionResponse Response(Subscription m)=>new(m.Id,m.PlanId,Snake(m.Status),m.PeriodStart,m.PeriodEnd,m.CancelAtPeriodEnd,m.CanceledAt);
    private static string Snake(SubscriptionStatus s)=>s switch{SubscriptionStatus.PastDue=>"past_due",_=>s.ToString().ToLowerInvariant()};
    private static IReadOnlyDictionary<string,long> ParseLimits(string json)=>JsonSerializer.Deserialize<Dictionary<string,long>>(json)
        ?? throw new SubscriptionUnavailableException();
    private static string Hash<T>(T value){var bytes=SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));return Convert.ToHexString(bytes).ToLowerInvariant();}
    private static void ValidateIdentity(SubscriptionIdentity i,Guid org){ArgumentNullException.ThrowIfNull(i);i.Validate();Require(org,nameof(org));}
    private static void ValidateMutation(SubscriptionIdentity i,Guid org,Guid id,Guid op){ValidateIdentity(i,org);Require(id,nameof(id));Require(op,nameof(op));}
    private static void Require(Guid id,string name){if(id==Guid.Empty)throw new ArgumentException("Identifier is required.",name);}
    private readonly record struct SubRow(SubscriptionResponse Response,long Version);
    private sealed record PlanData(Guid Id,string Code,string Name,decimal Price,string Currency,string Interval,IReadOnlyDictionary<string,long> Entitlements,bool Active);
}
