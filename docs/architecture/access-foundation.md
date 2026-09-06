# Identity, membership and branch reads

Status: the first protected read-only vertical slice. This is not a complete identity,
session, Owner, MFA, audited provisioning or client implementation.

## HTTP contract

GET /api/v1/organizations/{organizationId}/branches requires JWT authentication and
a persisted branches.view grant. pageSize is 1–100, default 50; after is the previous
response's nextCursor UUID. The response contains items with id, businessId, regionId,
code, name and timeZoneId, plus nullable nextCursor. Ordering follows PostgreSQL UUID
ordering. Cursors never authorize access; each page rechecks current permissions.
Pagination is not a frozen snapshot: legitimate changes may occur between requests.

GET /api/v1/organizations/{organizationId}/branches/{branchId} returns only an
authorized active, configured branch. Both nonexistent and inaccessible branches
return 404 after membership authorization. Missing active membership or branches.view
grants produces 403. Invalid/missing tokens produce 401 with a Bearer challenge.
Invalid queries return 400, limits 429, database unavailability 503. Unknown query
keys and repeated parameters are rejected. See [OpenAPI](../api/openapi.json).

## Trust boundary

Only validated issuer and subject are used as identity. Token role, permission,
tenant and branch claims do not grant business access. The organization in the URL
is a requested filter; membership, active state, validity and scope come from the database.

Each read uses a separate ReadCommitted transaction and parameterized set_config
calls with local=true. Membership RLS checks both organization and issuer/subject.
The actual branch query rechecks membership and grants within its own statement,
so a revocation committed between authorization and that statement is respected.
Already-running statements cannot retroactively revoke their snapshots. New requests
do not reuse stale permission caches.

Organization, business, optional region and branch must be active. Supported grants
are organization, business, region or branch. Terminal/warehouse/own/platform scopes
are not implemented by this small read API and cannot be granted here. A PlatformOwner
role string does not automatically expose tenant data.

Every business read checks the runtime role's privileges and ENABLE/FORCE RLS on
the six protected tables. Runtime cannot modify memberships or grants. Its GUC
settings are mutable, so RLS is not an independent authentication boundary against
SQL injection or stolen database credentials. Parameterized SQL, least privilege
and validated HTTP identity work together.

## Configuration

Use environment variables or the selected managed secret store, never committed secrets:

- Authentication__Authority: exact HTTPS issuer/authority URL.
- Authentication__Audience: audience dedicated to this API.
- ConnectionStrings__Application: restricted salekhpos_runtime connection,
  bounded pool/timeouts and SSL Mode=VerifyFull in production.

The JWT profile validates signature, issuer, audience and expiry with 30 seconds
of clock skew; typ must be at+jwt and algorithms are RS256, PS256 or ES256.
ID tokens, HMAC and unsigned tokens are rejected. The selected OIDC provider must
prove compatibility in a real sandbox. Current tests provide test-only RSA keys
and metadata to the real JwtBearer pipeline; they do not create a production
token issuer or login service.

Database certificate/hostname validation is mandatory outside loopback
Development/Testing. Maximum pool size is at most 100; connection and command
timeouts are 1–30 seconds. Multiplexing, disabled pooling and NoResetOnClose
are rejected. Production API TLS termination, trusted proxies and AllowedHosts
must be configured explicitly. Forwarded headers are not trusted by default.

## Failure, observability and performance boundaries

Queries read at most 100+1 rows; the extra row determines nextCursor. They do not
load all grants into memory or compute unbounded total counts. A list request uses
four statements: runtime safety, context, authorization and data, plus transaction
begin/commit. This is not a throughput benchmark. Realistic query-plan and load
verification remain performance gates.

Initial per-process limits allow 300 requests per IP per minute and 64 concurrent
business requests, with no waiting queue. These are safeguards, not an SLA.
A multi-replica deployment and shared-NAT stores require an appropriate edge or
distributed policy. Database errors do not trigger unlimited automatic retries.
Cancellation or exceptions dispose the transaction; reused connections do not
retain tenant/identity context.

Responses use no-store, nosniff and a server trace ID. Exception messages, SQL,
payloads and credentials are not included in public errors or the application's
failure log; status, exception class and trace ID remain available.
Complete telemetry exporters, alerting and security audit are still open.

## Migration and restoration

Historical migration 001 remains unchanged. Migration 002 adds business/region/
timezone and Unicode invariants; 003 adds memberships, grants and RLS.
Legacy records are not deleted. The STORED generated column, indexes and table
constraints in 002 may lock or rewrite large tables; do not apply it to a live
production-sized database without a tested migration plan.

Recovery must not consist of destructive DROP commands. Use compatible application
rollback, reviewed roll-forward or a separately restored backup as appropriate.
The runner applies migrations/regressions, makes a pg_dump custom-format backup,
restores it into a separate database and runs application tests there.
This proves a local logical restore, not production PITR, encrypted/cross-region/
immutable backups, separate backup credentials, HA failover or measured RPO/RTO.

References: [Microsoft JWT validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0),
[Npgsql transactions/pooling](https://www.npgsql.org/doc/basic-usage.html),
[Npgsql connection options](https://www.npgsql.org/doc/connection-string-parameters.html).
