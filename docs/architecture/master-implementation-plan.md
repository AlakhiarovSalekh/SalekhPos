# SalekhPos master implementation plan

Established September 6, 2026; translated and updated September 7.
This is the current execution plan. Earlier design documents retain historical
context. The complete source index is in master-specification-coverage.md;
unchanged source attachments are under ../requirements/.

The accepted, master-document-aligned monorepo layout is defined in
[repository-structure.md](repository-structure.md). Directories are created with
their first maintained artifact rather than as empty placeholders.

## Current State Assessment

At the start of this request, the repository contained a .NET 10 modular foundation:
two health endpoints, exact Money values, Organization/Branch types, the first
PostgreSQL migration and xUnit/SQL tests. The API did not connect to PostgreSQL;
readiness deliberately returned 503. There were no real external integrations.

Reusable work included precision checks, Unicode validation, tenant composite keys,
forced RLS, a restricted runtime role and pinned SDK/packages. Source lives in
OneDrive; live databases and build artifacts must stay outside it. All original
work was untracked and is preserved. The user subsequently authorized publication
of verified milestones through coherent Git commits.

Initial gaps included authentication/membership, permission scopes, business/region
hierarchy, explicit branch timezones, application connection pooling, auditing,
concurrency, CI and recovery tests. Functional product modules and client applications
were absent. Database and C# name validation differed; the simplified organization
to branch model did not satisfy the new multi-business requirement.

Current implemented changes and evidence are recorded in ../development/progress.md.
The baseline assessment above must not be confused with current execution status.

## Target Architecture

- ASP.NET Core/.NET 10 modular monolith with clear boundaries and separate workers.
- PostgreSQL authoritative data, tenant composite FKs, RLS, least-privilege roles,
  parameterized queries, transactions, bounded connection pools and query timeouts.
- React/TypeScript management web; Flutter native Windows/macOS/Linux/iOS/Android
  clients with platform adapters. No existing Flutter Web implementation is discarded.
- Native SQLite sale and outbox in one local transaction; synchronization completes
  only after durable server acknowledgement. Browser offline claims require separate proof.
- OIDC, native PKCE and future web BFF. Validated access tokens, server-controlled
  membership/scope/session/device revocation; distinct Owner MFA/reauth/audit boundary.
- Organization → Business → optional Region → Branch; flexible warehouse/device and
  user assignments that do not collapse multiple businesses or branches into one.
- Permissions, scope, subscription entitlements, country rules and device capabilities
  remain separate checks; all applicable conditions must pass.
- Exact decimal-string money/quantities in contracts; historical price/tax/FX snapshots;
  durable stock movements. Never hold a database transaction open across provider calls.
- PostgreSQL outbox/inbox and workers initially; add caches, brokers, search services,
  read replicas or analytical databases only when evidence justifies them.
- Safe structured logs, trace IDs, metrics and dependency health. No unmeasured capacity claims.
- Production TLS, managed secrets, redundant API/database, PITR, encrypted independently
  protected backups and restore/DR drills. Provider/region/budget remain undecided.

## Gap Analysis

1. P0: protected data requires authentication, active membership, scope and RLS together.
   No business endpoint is exposed without the applicable controls.
2. P0: financial effects must be durable, atomic, audited and idempotent. Money is only
   a small value-object foundation, not a completed pricing or checkout engine.
3. P0: prove tenant/branch boundaries in HTTP, pools, workers, caches, exports, sync and files.
4. P1: explicit hierarchy, timezone, currency/tax policy and versions; remove hidden
   assumptions tied to a single country or store configuration.
5. P1: reproducible build, analysis, tests, migrations, dependency/secret scans and recovery.
6. Track every remaining source section. Documentation, interfaces and mocks are not
   completed product features.

## Implementation Roadmap

| Phase | Scope | Required evidence |
|---|---|---|
| 0 | Repository, SDK/packages, CI, secrets | Locked restore, build, format, tests and scans |
| 1 | Contracts, errors, validation, logs/traces | Safe ProblemDetails, no sensitive disclosure, module boundaries |
| 2 | Identity, membership, hierarchy, permissions, Owner | Forged/expired token, tenant/branch attack, revocation and pool tests; MFA/Owner separately |
| 3 | Country, currency, timezone, tax framework | DST, rounding, snapshots, locale/RTL contracts |
| 4 | Catalog, variants, barcodes, units | Tenant uniqueness, fractional quantities, bounded indexed lookup |
| 5 | Stock ledger, locations, batches/serials/counts | Atomicity, concurrent changes, traceable adjustments and disposition |
| 6 | Pricing and promotions | Deterministic stacking, approval limits and server calculations |
| 7 | Registers, shifts, carts, cash sales, receipts | Atomic sale/stock/cash/outbox, retry/crash and client workflow |
| 8 | Payments, split tender, refunds | Provider idempotency, uncertain states/reconciliation, refund limits |
| 9 | Local database and synchronization | Restart, duplicates, checkpoints, conflicts, disk/network failure |
| 10 | Procurement, suppliers and receiving | Partial delivery, approvals, atomic receipt/stock |
| 11 | CRM and loyalty | Consent, tenant isolation, duplicate redemption, credit ledger |
| 12 | Employees and approvals | Scope, thresholds, expiry, requester/approver separation |
| 13 | Reporting and finance | Defined metrics, timezone, pagination, checkout workload isolation |
| 14 | Subscriptions | Entitlements/limits, trial/grace, regional billing, safe downgrade |
| 15 | Platform administration | Owner escalation protection, audited support and health |
| 16 | Hardware and external integrations | Real device/provider sandbox and country acceptance |
| 17 | Additional hardening | Load/stress/chaos, HA, restore, measured RPO/RTO and independent security review |

Work proceeds through small vertical changes within each phase. Relevant build,
analysis, tests, authorization/RLS, migration, logging and integrity checks accompany
each meaningful change. Repeating unchanged tests without a reason is unnecessary.

## First protected vertical slice

The first protected API reads bounded branch lists. Validated issuer/subject resolves
to active persisted membership; organization/business/region/branch grants are enforced
server-side. Client target IDs never prove permission. Pool context is transaction-local.
This slice does not complete membership/role provisioning, Owner bootstrap or login UI.
Missing configuration keeps access closed.

## Open decisions

The initial market is probably Georgia based on user history, but the global core
must not hardcode it. OIDC provider, hosting/region/budget, store type, fiscal/bank/
printer/scale models, legal retention and certification remain unknown. Continue
independent implementation and tests without inventing those policies. Production
integration claims require actual provider/device evidence.
