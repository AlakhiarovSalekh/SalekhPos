# ADR 003: Separate platform authority and operator bootstrap

Status: Accepted, September 7, 2026.

The platform's original owner must exclusively authorize additional Super Admins.
Tenant memberships, tenant permission grants and token role names cannot establish
this authority. The SystemAdministration module owns a separate registry and audit
schema with Domain, Application, Contracts, Infrastructure and API assemblies.
Architecture CI enforces their dependency direction. No other module tables are
queried by this module.

Use an out-of-band operator CLI to bind the original owner's verified OIDC issuer
and immutable subject. There is no HTTP bootstrap endpoint, signup claim or default
root identity. A singleton partial index and database triggers protect the original
root. Bootstrap retry requires the same operation ID and payload; replacement is
rejected even when repeated with privileged deployment credentials.

Runtime may execute narrow database functions but cannot read or write registry or
audit tables directly. Functions run as a dedicated non-login, non-superuser role,
with forced RLS, schema-qualified references, fixed search_path and no PUBLIC
execution. A separate bootstrap role alone receives the bootstrap function grant.
Keep both roles out of runtime membership. Root authorization and audit insertion
occur inside the same database transaction. This deliberately uses constrained
stored functions instead of broad table-write privileges in application code.

Creation and revocation require the persisted original root plus a provider-agreed
MFA assurance value in the signed API token, and auth_time within five minutes
(with 30 seconds of future clock tolerance). Empty assurance configuration denies
mutation. Role and amr claims alone are insufficient. Freshness is checked at HTTP,
application and database boundaries, including after acquiring the operation lock.
These are security controls, not evidence that a production identity provider is
configured or that MFA enrollment/login flows have been delivered.

Use operation UUIDs and immutable response snapshots for idempotency. Reusing an ID
with different inputs conflicts. Retrying creation after later revocation returns
the original response snapshot and never reactivates authority. Reauthorization
uses a new operation and a new registry record; old records remain historical.
A single transaction advisory lock serializes rare platform registry mutations;
ordinary tenant requests do not acquire it. No broker/outbox is added because no
other module currently consumes platform authority changes.

Root has no implicit tenant-data access. Additional Super Admins have no implicit
permission to create more administrators or use unimplemented platform features.
Future platform capabilities must define explicit policies rather than a global
role-name bypass. Readiness currently describes branch-read capability only.

Database administrators remain a trusted operational boundary: they could change
functions or triggers. This is not a claim of cryptographic tamper resistance.
Audit export/retention, deployment-role management and provider account recovery
must be implemented before production operation. Recovery restores the original
IdP identity; there is deliberately no convenient root-replacement endpoint.

Primary references: [PostgreSQL SECURITY DEFINER requirements](https://www.postgresql.org/docs/current/sql-createfunction.html#SQL-CREATEFUNCTION-SECURITY),
[OIDC acr and auth_time semantics](https://openid.net/specs/openid-connect-core-1_0.html#IDToken).
The selected provider must explicitly supply equivalent assurance in API access
tokens; OIDC ID tokens themselves remain rejected by the SalekhPos API.
