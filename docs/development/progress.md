# Implementation progress

## September 7, 2026 — current charter and durable token revocation

The newly supplied Master Architecture Prompt was read in full: 4,720 lines and
168 numbered sections. A byte-identical copy is committed as
`docs/requirements/master-architecture-charter.md`; CI checks its SHA256 and every
section range, alongside the 296 earlier sections. ADR 002 records precedence,
repository migration, preserved terminology and remaining module-boundary debt.
`AGENTS.md` records the continuation entry point and latest language directions.

### Implemented and verified

- Moved working code into backend/src/Bootstrapper, BuildingBlocks, Modules and
  backend/tests. Updated solution, references, scripts, dependency paths and docs.
  Existing migration 001-003 bytes, public API routes and business data are preserved.
- Identity-owned POST /api/v1/identity/revoke-current-token durably revokes the
  authenticated current API credential. Every authenticated request checks it.
  New module-owned migration 004 participates in both Windows and Docker runners.
- Revocation and its immutable audit are one atomic row. SHA256 fingerprints cover
  validated JWT signing input so alternate signature encodings cannot bypass
  revocation. No raw credentials persist. Identity-scoped forced RLS, restricted
  insert columns, runtime checks and pooled transaction-local context are enforced.
- Repeat/concurrent writes are idempotent; requests using a revoked token receive
  401. Host restart preserves revocation. Another user's or distinct token's access
  is unaffected. Missing revocation storage fails closed and readiness stays down.
- Full Windows CI gate passed: 121 domain/configuration/HTTP tests and 35 real
  PostgreSQL integration tests (156 total), zero failures/skips; Release build with
  zero warnings/errors; locked restore, formatting/analyzers, requirement integrity,
  architecture checks, Gitleaks and NuGet advisory checks passed.
- Migrations 001-004 and all SQL regressions passed; a logical backup was restored
  into another database before .NET integration tests. Native test data remains
  outside the repository and the disposable PostgreSQL instance was stopped.

### Precise continuation point

This is a verified backend milestone, not a complete POS application. Next implement
Identity provisioning and provider integration with server-owned platform authority:
choose/test an OIDC provider, establish Root Super Admin bootstrap with MFA, and
add audited tenant-owner provisioning without public platform-role elevation.
Provider logout and refresh-token/session/device revocation are still open; the
new endpoint revokes one API credential only. No production identity provider or
hosting credentials have been configured. Continue independent policy/domain work
before requiring external provider credentials.

Then implement store management and catalog as complete authorized/audited vertical
slices, followed by register/shift, atomic sales/payments, inventory event handling,
native offline storage/sync and hardware proofs. Web/Desktop/Mobile/Kiosk remain
unimplemented. Do not create their target trees without actual functionality.

Before production: audit retention/export, provider revocation, trusted TLS/proxy
configuration and operating controls remain release gates. Never roll back to an
API build lacking revocation checks while revoked tokens remain valid. Details:
`docs/architecture/token-revocation.md`.

Published implementation commit `e77de5b` to GitHub main. Hosted Linux Quality run
[34091471890](https://github.com/AlakhiarovSalekh/SalekhPos/actions/runs/34091471890)
passed the complete gate, including Docker PostgreSQL migration/restore and tests.
Earlier open Dependabot PRs are outside this implementation.


## September 7, 2026 — verified identity and branch-read foundation

Both master documents were read completely: 7,412 lines, 296 main sections and
84 subordinate headings. Byte-identical copies are preserved with SHA256 hashes.
CI verifies that every main section appears once with its exact source range.
The current-state assessment, target architecture, gaps and 18-phase roadmap are
in [master-implementation-plan.md](../architecture/master-implementation-plan.md).

Previous conversation requirements were recovered: six platforms, global languages
and currencies, small/medium/large stores, a strong foundation and continuing work
through safe milestones. The user explicitly authorized GitHub publication and
subsequently required English for all work. Project documentation is translated
before its initial publication; original requirement attachments stay unchanged.

### Implemented behavior

- Organization → Business → optional Region → Branch, explicit IANA timezone,
  tenant-safe composite foreign keys and matching Unicode validation.
  Legacy data is retained; unknown business/timezone values are not invented.
  Unconfigured branches are excluded from the read API.
- Real JwtBearer signature, issuer, audience, expiry and access-token-type validation.
  Forged role/tenant claims do not grant business access. Active persisted membership
  and organization/business/region/branch grants protect two versioned read endpoints.
- Bounded parameterized SQL, transaction-local tenant/identity context, runtime/RLS
  checks, keyset pagination, cancellation/rollback, safe ProblemDetails and trace IDs.
- CI gates for original requirement integrity, project boundaries, locked restore,
  build, formatting/analyzers, Gitleaks, NuGet advisories, SQL regressions, logical
  backup/restore and real database integration tests.
- Login UI, a real OIDC provider, Owner bootstrap/MFA, session/device revocation,
  role provisioning and security auditing are not complete. Sales, inventory,
  payments, offline synchronization and client applications remain open.

### Verification evidence

Windows, .NET SDK 10.0.400, PostgreSQL 18.6:

- 114 domain/configuration/HTTP tests and 27 integration tests: 141 passed,
  zero failed and zero skipped.
- Migrations 001–003 and their SQL regression suites passed.
- Release build: zero warnings/errors. Formatting and static analysis passed.
- Gitleaks found no worktree leaks. NuGet reported no known vulnerable direct
  or transitive packages at the time of the scan.
- Integration tests use the real JWT validator and PostgreSQL: invalid signature,
  issuer, audience, expiry and ID token; BOLA; forged role claims; inactive/expired/
  revoked memberships; every implemented scope; pagination; sequential and
  concurrent tenants on one physical connection; failure/cancellation context cleanup.

The test runner creates a logical pg_dump backup, restores it into a separate
database and runs application tests there. A pre-migration legacy record retains
its ID, code, name, timestamp and unconfigured status through migration/restore.
This does not establish production PITR/HA, encrypted or immutable cross-region
backups, zero RPO or a production recovery SLA.

### Remaining work

Initial GitHub publication is prepared as logical commits under the user's
authorization. Hosted CI must be confirmed separately from local evidence.
Next: complete identity/provisioning/audit boundaries, then global configuration
and catalog. Provider, hosting, device and fiscal decisions remain open.
See [access-foundation.md](../architecture/access-foundation.md) for precise limitations.

## September 6, 2026 — organization foundation

- Persisted the user's standing quality and continuing-work directions.
- Added Organization and Branch models with nonempty identifiers, branch-code
  validation, Unicode names and immutable tenant/branch associations.
- Historical .NET result: 39 passed, zero failed/skipped.
- Added the first PostgreSQL migration and isolation SQL tests.
- Docker Desktop existed but WSL was unavailable; a portable Windows PostgreSQL
  installation supplied a disposable real database.
- PostgreSQL 18.6 isolation tests passed: tenant A/B read/write boundaries, valid
  branch creation, duplicate-code rejection, denied deletion/RLS bypass and
  context cleanup after commit/rollback. The temporary server was stopped.
- Initialized the local codex/foundation branch and configured the supplied GitHub
  origin. No commit or push had occurred at that point.
- Authentication, branch scopes and actual connection-pool behavior were still open
  at that historical milestone; the later work above addresses a bounded subset.

## September 6, 2026 — first backend foundation

The user authorized implementation in this project directory. Earlier architecture
documents describe the design before implementation; their original “not implemented”
status is historical. This progress file is the current execution record.

- Installed .NET SDK 10.0.400 in a temporary development directory without changing
  the system .NET 8 installation.
- Created the solution, API, SharedKernel and xUnit project.
- Enabled nullable analysis, warnings as errors and deterministic build settings.
- Added immutable Money addition/subtraction without implicit rounding. Unrepresentable
  decimal results are rejected. Negative values support accounting reversals;
  this value object does not define sale limits or settlement policy.
- Currency syntax only was validated. Supported currencies, tax, unit-price precision,
  allocation and payment rounding were not implemented.
- Separated process liveness from business readiness; readiness remained closed.
- Added package locks and kept SDK/build caches outside OneDrive.

Historical verification: Windows x64, SDK 10.0.400, runtime 10.0.11; Release API
build succeeded with zero warnings/errors; 22 initial tests passed. Those tests
covered exact decimal arithmetic, precision retention, currency syntax/mismatch,
nulls, negative results, immutability, overflow, precision loss, large-value
cancellation, equality and health endpoints. These narrow results were not a
product correctness guarantee or production-readiness claim.

The historical next flow was organization/access → catalog → shifts → atomic cash
sale/inventory → idempotency → receipt, followed by native/offline/hardware proofs.
The master roadmap expands this flow while retaining the same integrity priorities.
