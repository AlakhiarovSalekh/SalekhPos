using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class AccessFixture : IAsyncLifetime
{
    public const string Issuer = "https://identity.example.test";
    public const string Audience = "salekhpos-api";
    public Guid OrganizationA { get; } = Guid.NewGuid();
    public Guid OrganizationB { get; } = Guid.NewGuid();
    public Guid BusinessA { get; } = Guid.NewGuid();
    public Guid BusinessA2 { get; } = Guid.NewGuid();
    public Guid BusinessB { get; } = Guid.NewGuid();
    public Guid RegionA { get; } = Guid.NewGuid();
    public Guid BranchA { get; } = Guid.NewGuid();
    public Guid BranchA2 { get; } = Guid.NewGuid();
    public Guid BranchA3 { get; } = Guid.NewGuid();
    public Guid BranchB { get; } = Guid.NewGuid();
    public string RuntimeConnection { get; private set; } = null!;
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    private NpgsqlDataSource admin = null!;
    private readonly RSA rsa = RSA.Create(2048);
    private RsaSecurityKey Key => new(rsa) { KeyId = "integration-test-key" };

    public async Task InitializeAsync()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SALEKHPOS_TEST_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Run scripts/test-postgres.ps1 -RunDotnetTests to supply a disposable database. Integration tests never silently skip.");
        RuntimeConnection = Environment.GetEnvironmentVariable("SALEKHPOS_TEST_RUNTIME_CONNECTION")
            ?? throw new InvalidOperationException("Missing disposable runtime connection. Use the PostgreSQL test runner.");
        admin = NpgsqlDataSource.Create(adminConnection);
        await ExecuteAsync("""
            SELECT system_administration.bootstrap_root('51000000-0000-0000-0000-000000000001',
                $1,'platform-root','Integration fixture original root','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')
            """, Issuer);
        await using (var restored = admin.CreateCommand("""
            SELECT count(*) FROM organization.branches
            WHERE branch_id='90000000-0000-0000-0000-000000000002'
              AND organization_id='90000000-0000-0000-0000-000000000001'
              AND code='LEGACY' AND name='Legacy' || chr(9) || 'branch'
              AND created_at='2020-01-02T03:04:05Z' AND NOT is_configured
            """))
        {
            Assert.Equal(1L, await restored.ExecuteScalarAsync());
        }
        await ExecuteAsync("INSERT INTO organization.organizations(organization_id,name) VALUES ($1,'Tenant A'),($2,'Tenant B secret')", OrganizationA, OrganizationB);
        await ExecuteAsync("INSERT INTO organization.businesses(organization_id,business_id,code,name) VALUES ($1,$2,'A','Brand A'),($1,$3,'A2','Brand A2'),($4,$5,'B','Brand B')", OrganizationA, BusinessA, BusinessA2, OrganizationB, BusinessB);
        await ExecuteAsync("INSERT INTO organization.regions(organization_id,business_id,region_id,code,name) VALUES ($1,$2,$3,'WEST','West')", OrganizationA, BusinessA, RegionA);
        await ExecuteAsync("""
            INSERT INTO organization.branches(organization_id,business_id,branch_id,region_id,code,name,time_zone_id)
            VALUES ($1,$2,$3,$4,'A1','Branch A1','Asia/Tbilisi'),
                   ($1,$2,$5,NULL,'A2','Branch A2','America/New_York'),
                   ($1,$6,$7,NULL,'A3','Branch A3','Etc/UTC'),
                   ($8,$9,$10,NULL,'B1','Tenant B secret','Etc/UTC')
            """, OrganizationA, BusinessA, BranchA, RegionA, BranchA2, BusinessA2, BranchA3, OrganizationB, BusinessB, BranchB);
        await AddMembershipAsync("alice", OrganizationA, "branch", BusinessA, branch: BranchA);
        await AddMembershipAsync("owner", OrganizationA, "organization");
        await GrantAsync("owner", OrganizationA, "products.view");
        await GrantAsync("owner", OrganizationA, "products.create");
        await GrantAsync("owner", OrganizationA, "products.update");
        await GrantAsync("owner", OrganizationA, "inventory.view");
        await GrantAsync("owner", OrganizationA, "inventory.adjust");
        await GrantAsync("owner", OrganizationA, "pricing.view");
        await GrantAsync("owner", OrganizationA, "pricing.manage");
        await GrantAsync("owner", OrganizationA, "sales.complete");
        await GrantAsync("owner", OrganizationA, "sales.view");
        await GrantAsync("owner", OrganizationA, "payments.view");
        await GrantAsync("owner", OrganizationA, "sales.refund");
        await GrantAsync("owner", OrganizationA, "sales.void");
        await GrantAsync("owner", OrganizationA, "stores.manage");
        await GrantAsync("owner", OrganizationA, "stores.view");
        await GrantAsync("owner", OrganizationA, "shifts.open");
        await GrantAsync("owner", OrganizationA, "shifts.view");
        await AddMembershipAsync("manager", OrganizationA, "business", BusinessA);
        await AddMembershipAsync("regional", OrganizationA, "region", BusinessA, RegionA);
        await AddMembershipAsync("bob", OrganizationB, "organization");
        await AddMembershipAsync("none", OrganizationA, null);
        await AddMembershipAsync("revoked", OrganizationA, "organization");
        await ExecuteAsync("UPDATE access.memberships SET is_active=false WHERE organization_id=$1 AND subject='revoked'", OrganizationA);
        await AddMembershipAsync("expired", OrganizationA, "organization");
        await ExecuteAsync("UPDATE access.memberships SET valid_from=now()-interval '2 days', valid_until=now()-interval '1 day' WHERE organization_id=$1 AND subject='expired'", OrganizationA);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Authentication:Authority", Issuer);
            builder.UseSetting("Authentication:Audience", Audience);
            builder.UseSetting("Authentication:PrivilegedAcr", "urn:salekhpos:test:mfa");
            builder.UseSetting("ConnectionStrings:Application", RuntimeConnection);
            builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Test-only provider metadata and public key. The real JwtBearer
                // signature/lifetime/issuer/audience validation pipeline still runs.
                var metadata = new OpenIdConnectConfiguration { Issuer = Issuer };
                metadata.SigningKeys.Add(Key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
            }));
        });
    }

    public async Task AddMembershipAsync(string subject, Guid organization, string? scope,
        Guid? business = null, Guid? region = null, Guid? branch = null)
    {
        var member = Guid.NewGuid();
        await ExecuteAsync("INSERT INTO access.memberships(organization_id,membership_id,issuer,subject) VALUES($1,$2,$3,$4)", organization, member, Issuer, subject);
        if (scope is not null)
        {
            await ExecuteAsync("""
                INSERT INTO access.permission_grants(organization_id,grant_id,membership_id,permission,scope_kind,business_id,region_id,branch_id)
                VALUES($1,$2,$3,'branches.view',$4,$5::uuid,$6::uuid,$7::uuid)
                """, organization, Guid.NewGuid(), member, scope, business ?? (object)DBNull.Value, region ?? (object)DBNull.Value, branch ?? (object)DBNull.Value);
        }
    }

    public Task GrantAsync(string subject, Guid organization, string permission) => ExecuteAsync("""
        INSERT INTO access.permission_grants(organization_id,grant_id,membership_id,permission,scope_kind)
        SELECT organization_id,$1,membership_id,$2,'organization' FROM access.memberships
        WHERE organization_id=$3 AND issuer=$4 AND subject=$5
        """, Guid.NewGuid(), permission, organization, Issuer, subject);

    public string Token(string subject = "alice", string? issuer = null, string? audience = null,
        bool expired = false, bool badSignature = false, string type = "at+jwt", bool forgedClaims = false,
        string? acr = null, long? authTime = null)
    {
        using var otherKey = badSignature ? RSA.Create(2048) : null;
        var key = otherKey is null ? Key : new RsaSecurityKey(otherKey) { KeyId = Key.KeyId };
        var claims = new List<Claim> { new("sub", subject), new("jti", Guid.NewGuid().ToString()) };
        if (acr is not null) { claims.Add(new Claim("acr", acr)); }
        if (authTime is not null) { claims.Add(new Claim("auth_time", authTime.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)); }
        if (forgedClaims)
        {
            claims.AddRange([new("role", "PlatformOwner"), new("permission", "branches.view"), new("organization_id", OrganizationB.ToString())]);
        }
        var token = new JwtSecurityToken(issuer ?? Issuer, audience ?? Audience, claims,
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(expired ? -5 : 5), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        token.Header["typ"] = type;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task ExecuteAsync(string sql, params object[] parameters)
    {
        await using var command = admin.CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        }
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql, params object[] parameters)
    {
        await using var command = admin.CreateCommand(sql);
        foreach (var parameter in parameters) command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Expected a scalar result."));
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) { await Factory.DisposeAsync(); }
        if (admin is not null) { await admin.DisposeAsync(); }
        rsa.Dispose();
    }
}
