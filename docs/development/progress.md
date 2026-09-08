# Implementation progress

## September 9, 2026 — authorized sale reads and receipt projection

Added branch-scoped sale and receipt endpoints backed only by immutable completed-sale
snapshots. Both operations require the persisted `sales.view` permission and repeat
the active organization, business, branch and membership checks before reading.
RLS remains forced on the underlying sale and line records. Unknown sale identifiers
return 404 only inside an authorized scope; callers without permission receive 403.

Receipt values are projected from the unit price, currency, tax mode, tax rate and
calculated totals stored when the sale completed. Historical receipts therefore do
not change when pricing or tax configuration changes. The focused disposable native
PostgreSQL run passes 165 unit/configuration/HTTP tests and 64 integration tests with
zero failures or skips. Next establish the separate Payments module and associate an
immutable cash payment with each completed cash sale.

## September 9, 2026 — atomic cash-sale completion slice

Implemented Sales as five module projects in the supplied Sales structure. The
first versioned completion endpoint accepts a bounded product/quantity cart and
cash tender. It resolves active branch/base prices at one database timestamp and
persists immutable sale headers and lines with the exact applied price, currency,
tax mode, rate, net, tax and gross values. Six-decimal half-even calculation is a
single domain policy; mixed currencies and insufficient cash fail before writes.

Migration 010 adds tenant-composite sales, line and outbox records with forced RLS
and insert/select-only runtime grants. Completion requires the persisted,
branch-scoped `sales.complete` permission. One database transaction writes the
sale, lines, inventory sale movements and `sales.sale_completed.v1` outbox
message. Transaction-scoped advisory locks serialize each product/branch stock
decision, preventing concurrent overselling. UUID operation locks make exact
retries return the original sale without another stock movement; changed retries
return 409. Completed financial records cannot be updated or deleted by runtime.

The focused native PostgreSQL migration/restore run passes 165 unit/configuration/
HTTP tests and 63 integration tests with zero failures or skips. Coverage includes
tax arithmetic, cash/change, atomic persistence, replay, insufficient stock,
authorization, tenant isolation and concurrent oversell prevention.

## September 8, 2026 — deterministic scheduled pricing slice

Implemented Pricing as five module projects in the supplied module structure.
The versioned API schedules organization-base or branch-specific product prices
and resolves the deterministic price at an explicit UTC instant. Branch prices
override organization prices. Amount and tax-rate precision, ISO-style uppercase
currency syntax, inclusive/exclusive tax mode and half-open effective intervals
are enforced in both domain code and PostgreSQL.

Migration 009 adds tenant-composite product/branch references, forced RLS, minimum
runtime grants and database exclusion constraints that reject concurrent overlapping
prices for the same product and scope. Stored prices are immutable to the runtime
role and retain issuer, subject, tax policy and validity for historical correctness.
Scheduling requires `pricing.manage`; resolution requires `pricing.view`, active
persisted membership and active organization/store hierarchy. Writes use UUID
idempotency keys and changed replays return 409.

The focused native PostgreSQL migration/restore run passed 163 unit/configuration/
HTTP tests and 60 integration tests with no failures or skips. SQL and HTTP coverage
includes immutable rows, tenant isolation, overlapping-window rejection, safe replay,
branch precedence and unauthorized access. Next integrate Catalog, Pricing and
Inventory into an atomic cash-sale workflow that persists applied price/tax snapshots.

## September 8, 2026 — immutable inventory ledger slice

Implemented Inventory as five module projects with a branch-scoped stock API and
an immutable PostgreSQL movement ledger. Receipts, inward/outward adjustments,
sales and returns have explicit direction semantics, positive six-decimal quantity
validation and canonical UTC microsecond timestamps. Movement creation requires
`inventory.adjust`; stock reads require `inventory.view`. Both permissions are
resolved from active persisted membership and organization/business/branch scope.

Migration 008 adds composite tenant foreign keys to branches and products, forced
RLS, minimum runtime grants and immutable rows because the runtime can insert and
select but cannot update or delete movements. A UUID idempotency key safely replays
the original operation; a changed replay returns 409. Current stock is derived from
the ledger and paged by product ID. Database and HTTP tests verify tenant isolation,
authorization, replay behavior, immutability and exact stock totals.

Verified against disposable PostgreSQL 18.6 with logical backup/restore: 158 unit,
configuration and HTTP tests plus 58 integration tests passed with zero failures or
skips. Release build completed with zero warnings or errors. Next implement tenant
pricing, tax-inclusive/exclusive price rules and effective-date conflict handling,
then use Catalog, Pricing and Inventory in the atomic sales workflow.

## September 8, 2026 — tenant-isolated product catalog slice

Implemented Catalog as five real module projects: Domain, Contracts, Application,
Infrastructure and API. The first versioned product API can create and list product
definitions. Product identity, SKU, name, unit and numeric barcode invariants are
enforced in both the domain and PostgreSQL. Creation requires an organization-scoped
`products.create` grant; reads require `products.view`. Client claims do not grant
tenant access.

Migration 007 adds the catalog schema, composite tenant keys, per-tenant SKU/barcode
uniqueness, forced RLS, minimal runtime privileges and an audit trigger that records
the validated issuer and subject without exposing audit rows to the runtime role.
Creation requires a UUID `Idempotency-Key`; exact sequential and concurrent replays
return the original product, while changed payloads and duplicate SKUs return 409.
Runtime-role elevation and missing forced RLS fail closed.

Release compilation and architecture checks pass for 21 projects. The disposable
PostgreSQL migration/restore/application run passes 149 unit/configuration/HTTP tests.
The product API also supports lookup by ID or barcode and organization-authorized
updates/deactivation. Updates use the persisted row version, return 409 for stale
writes or barcode conflicts, and create an immutable audit record. The latest run
passes 56 integration tests with zero failures or skips. Next implement Pricing and
the inventory movement ledger. The current web session is not yet a BFF for these
business APIs.

## September 8, 2026 — web identity vertical slice verified

Implemented the first working browser application in `apps/web`: public landing,
sign-in and authenticated workspace pages. Added a confidential OpenID Connect
authorization-code flow with PKCE in the ASP.NET bootstrapper. Browser code receives
only an HttpOnly session identifier; provider tokens are encrypted through ASP.NET
Data Protection and stored server-side in PostgreSQL. Login and logout mutations use
antiforgery and exact-origin validation. Production configuration requires HTTPS and
an explicit persisted, certificate-protected Data Protection key ring.

Migration 006 adds forced-RLS web sessions and an owner-controlled mutation audit.
Runtime access is scoped to the hashed session key, and logout deletes the durable
ticket so replaying a copied cookie fails. Added unit, PostgreSQL integration, SQL
regression and Playwright coverage. The native verification passed 142 unit/HTTP
tests, 50 real PostgreSQL integration tests and one real Keycloak 26.7.3 browser
login/logout test, all without skips. The Next.js production build, ESLint and
working-tree/Git-history Gitleaks scans passed. The native runner now fails immediately
when its Java or Keycloak runtime is absent instead of waiting for a readiness timeout.

This is a completed identity/web slice, not the complete POS. Next implement provider
MFA enrollment and step-up/session revocation, then audited tenant onboarding and the
first store/catalog/sales vertical slice. Business APIs are not yet exposed through
the browser session.

## September 7, 2026 — supplied final structure implemented

The newer explicit structure instruction supersedes incremental directory creation.
Preserved the full original in `docs/requirements/final-complete-file-structure.md`;
SHA256: `3648134C078BB553F7D322A4FC03E6750D700A3645F9E16F9F2F81D69E3B7942`.
All 1,543 supplied entries (1,340 directories, 203 files) are present and Git-tracked.
Keep markers preserve reserved directories after a clone. The source/manifest/type
checker is included in CI. Additional code/project/evidence files support the tree.

Moved the CLI to `tools/cli/SalekhPos.Cli`, split Identity into Application,
Infrastructure and API assemblies with `ITokenRevocations` dependency inversion,
split Authorization Application/Infrastructure, and moved hierarchy code into
Organizations.Domain. Moved Money, exception handling and tests to required paths.
Historical migrations now live in `database/migrations`; SQL regressions are in
`tests/integration/database`. Every moved migration blob remains identical.
`SalekhPos.sln` contains all 16 implemented projects. Package versions are managed
centrally. Updated references, namespaces, locks, native/Docker runners, Dependabot,
backend entry scripts and `.github/workflows/ci.yml`.

The complete Windows Native gate passed: 137 unit/configuration/HTTP tests and
48 real PostgreSQL integration tests; zero failures/skips. Release build passed
with zero warnings/errors. Formatting, architecture, original requirement hashes,
Gitleaks and NuGet advisory checks passed. All five migrations, SQL regressions,
logical restore and the relocated real root bootstrap CLI passed. The disposable
server stopped. Structure/Git tracking, PowerShell syntax and JSON parsing passed.
The incomplete local SDK and package cache were restored using the exact pinned
SDK and a dedicated temporary package cache. No production data was touched.

Implementation commit `aac5246` is published to GitHub main. Hosted Linux verification
[34121562942](https://github.com/AlakhiarovSalekh/SalekhPos/actions/runs/34121562942)
passed the complete Linux/Docker gate, including all 185 tests and the relocated CLI.

### Exact continuation and limitations

The path layout and existing-code relocation are complete. The complete POS is
not ready for handover. Requested client, hardware, operations and configuration
locations remain explicitly reserved. Their presence does not mean functioning
sales, inventory, offline synchronization, MFA enrollment or production deployment.
Unimplemented scripts and manual workflows fail explicitly instead of reporting
success. Implemented backend check workflows reuse the full quality gate.

Next implement real OIDC authorization-code + PKCE web login, MFA step-up and
provider session revocation in the supplied paths, followed by audited tenant
onboarding and the store/catalog vertical slice. Replace reservations with working
behavior without removing required paths. The full product goal remains active.

## September 7, 2026 — original-root authority and audited administration

The active goal remains the complete global, six-platform retail ecosystem. This
is one implemented security dependency, not a redefinition of the final goal.
The goal attachment was compared with the current charter; after normalizing
bullet markers and blank lines, its content is identical.

### Implemented and verified

- Added SystemAdministration with separate Domain, Application, Contracts,
  Infrastructure and API assemblies. CI enforces framework-independent core
  layers and allowed module-layer references.
- Added an operator-only root bootstrap executable and module-owned migration 005.
  Initial root authority is bound to a reviewed external issuer/subject, with no
  public bootstrap/signup route or production seed identity. Replacing/deleting/
  revoking the original root is forbidden. Separate bootstrap credentials are
  never read by the API or passed in CLI arguments.
- Added current platform-authority lookup and root-only registration/revocation
  of additional Super Admins. Persisted authority is separate from tenant roles;
  root receives no automatic tenant-data access. Additional admins cannot delegate.
- Mutation requires configured provider MFA assurance and recent auth_time.
  Missing assurance configuration fails closed. JWT signature/issuer/audience/
  lifetime/type and durable token revocation remain enforced.
- Atomic immutable audit, exact idempotent replay, conflicting-ID rejection and
  concurrent duplicate protection are implemented. API runtime has no registry/
  audit table privileges. Controlled functions use a non-login owner, fixed
  search_path, forced RLS and no PUBLIC execution. Database administrators remain
  a trusted operational boundary, not a claimed tamper-proof adversary boundary.
- Full Windows quality gate passed: 137 unit/configuration/HTTP and 48 real
  PostgreSQL integration tests, 185 total, zero failures/skips. Release build has
  zero warnings/errors. Formatting including informational analyzers, requirement
  hashes/section coverage, architecture, Gitleaks and NuGet advisory checks passed.
- Migration 005 SQL tests cover runtime bootstrap denial, tenant escalation,
  missing/stale MFA, immutable root/audit and rollback after injected audit failure.
  All five migrations and logical restore passed. The real bootstrap CLI was run
  against restored disposable PostgreSQL: initial commit, retry, root replacement
  rejection and runtime-credential rejection passed. The disposable server stopped.

A test exposed empty MFA configuration producing 500; fixed it to deny privileged
access. A redirected native PostgreSQL run held an inherited output handle after
pg_ctl exited; that specific disposable server was stopped, the attempt ended,
and the full gate was rerun normally. No production process or data was touched.

### Continue here

Published implementation commit `6a90e1b` to GitHub main. Hosted Linux Quality
[34117600524](https://github.com/AlakhiarovSalekh/SalekhPos/actions/runs/34117600524)
passed the full gate, including the real operator CLI and Docker PostgreSQL tests.
Next implement
a real OIDC provider integration and web login with authorization code + PKCE,
MFA enrollment/step-up and provider session/refresh-token revocation. The platform
policy currently validates signed test-provider assurance; it is not proof of an
actual production MFA flow. No real IdP account or root owner has been provisioned.
Select and validate the provider using current primary documentation, preserving
provider-independent business boundaries. User permission is not needed for
ordinary implementation or disposable local integration environments.

After identity flow, implement audited tenant-owner onboarding and store/catalog
vertical slices. Sales, purchasing, inventory, native clients, offline/sync,
hardware, reporting and remaining lifecycle capabilities are still required by
the active full goal. Production secret/hosting setup, audit retention/export,
durable denied-security events and original IdP account recovery remain open.

Contracts and bootstrap/runbook details: docs/architecture/platform-administration.md.
Architecture and trust decisions: docs/adr/003-platform-authority.md.


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
