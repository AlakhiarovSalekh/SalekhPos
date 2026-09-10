# Implementation progress

## September 10, 2026 — authenticated bounded desktop sync execution

Desktop synchronization now obtains a bearer access token for every HTTP request,
allowing the token provider to refresh credentials without persisting tokens in the
sale outbox. A bounded runner retries only network, timeout, rate-limit and server
failures with capped exponential backoff. Authentication and other deterministic
client failures surface immediately; cancellation remains honored. Because every
attempt rereads pending evidence, a partially successful batch safely resumes after
the last durably acknowledged message.

Acknowledgement validation and SQLite result transitions now require status/result
semantic agreement: only `applied/applied` is successful and bounded conflict codes
must be rejected. Contradictory, altered or unknown responses cannot remove pending
evidence. Focused tests cover per-request authorization, transient recovery, no retry
for unauthorized responses, mismatch retention, semantic contradictions and durable
pending-state preservation. Native CI passes structure and architecture for 70
projects, secret and dependency scanning, formatting, all PostgreSQL migrations and
backup/restore, 178 unit/configuration/HTTP/desktop tests and 72 integration tests
with zero failures or skips. Next add atomic local catalog/price/stock projections
and authenticated download orchestration so offline sale lines are constructed only
from synchronized server evidence.

## September 10, 2026 — durable desktop sale outbox and sync dispatch

Established the first functional desktop persistence boundary as separate Domain,
Application, Infrastructure and test projects. Cash-sale completion validates exact
six-decimal price, tax, quantity, currency and receipt arithmetic, then stores the
immutable sale, ordered line snapshots, canonical protocol-v1 payload, SHA-256 digest
and gap-free per-device outbox sequence in one immediate SQLite transaction. WAL,
full synchronous durability, foreign keys, busy timeout, strict tables and immutable
sale/line triggers protect crash recovery and evidence integrity. Exact completion
replay returns the original message; changed replay is rejected.

Pending messages are dispatched in device-sequence order through the versioned,
tenant-scoped HTTP endpoint. Local state changes only after the acknowledgement's
message, device, sale, sequence, protocol, type, digest, status and UTC acceptance
time match the pending evidence. Applied and deterministic rejected outcomes are
persisted idempotently; mismatched or unknown results remain pending. Native CI passes
structure and architecture for 70 projects, secret and dependency scanning, formatting,
all PostgreSQL migrations and backup/restore, 172 unit/configuration/HTTP/desktop tests
and 72 integration tests with zero failures or skips. Next wire authenticated desktop
composition and bounded retry scheduling, then add durable catalog/price/stock
projections required to build offline sale lines from synchronized server evidence.

## September 10, 2026 — atomic offline cash-sale materialization

Validated `sale.completed.v1` messages now produce their immutable completed sale,
cash payment trigger record, stock-ledger movements, line snapshots, sale outbox event
and durable sync result in one PostgreSQL transaction. Admission serializes device,
shift and product stock decisions; requires both `sync.ingest` and `sales.complete`;
binds the payload register to the device and still-open shift; verifies the historical
price/tax snapshot and current stock; and rejects future, duplicate or conflicting
financial evidence without partial writes.

Migration 029 adds append-only forced-RLS applied/rejected result evidence. Deterministic
business rejection advances the device checkpoint with a bounded result code so one
conflict cannot permanently block later messages; exact replay returns the persisted
decision and linked local sale ID without duplicating sale, payment, inventory or
outbox records. Integration coverage proves applied, shift-conflict, price-conflict
and duplicate-sale decisions, rejected-sale absence and replay stability. Existing
pre-migration accepted evidence remains readable. A simultaneous two-device test
proves the shared product lock permits exactly one sale against one available unit
and durably rejects the other as insufficient stock. Native CI passes structure and
architecture for 66 projects, secrets, dependencies, formatting, all migrations and
backup/restore, 167 unit/configuration/HTTP tests and 72 integration tests with zero
failures or skips. Next begin the durable desktop local-sale/outbox slice.

## September 10, 2026 — strict offline-sale financial envelope

Protocol-v1 `sale.completed.v1` admission now requires the complete immutable local
receipt snapshot: sale, shift and register IDs, UTC completion time, currency, cash,
totals and ordered price/tax line evidence. The parser rejects missing, duplicate or
unknown fields; noncanonical IDs/timestamps; invalid precision/ranges; duplicate
products; nonsequential lines; and any line, total or change arithmetic mismatch.
Invalid financial evidence cannot enter the durable sync ledger or advance its
device checkpoint.

Native CI passes structure and architecture for 66 projects, secrets, dependencies,
formatting, migration/restore, 167 unit/configuration/HTTP tests and 71 integration
tests with zero failures or skips. Next atomically persist the validated snapshot as
the existing immutable sale/payment/inventory/outbox records, check the recorded
shift/register relationship and add durable applied-or-rejected sync result evidence.

## September 10, 2026 — actor-bound sync evidence reads

Hardened protocol-v1 ingestion so only the authenticated issuer/subject that
registered an active device can advance or inspect that device's checkpoint.
Payloads must now be valid bounded-depth JSON objects before hashing or durable
admission. This closes the earlier gap where another authorized operator could
name a foreign device identifier, while preserving exact same-envelope replay.

Added authorized message detail and bounded newest-first history endpoints without
exposing stored payload bodies. History uses strict 1–100 sizing and sequence-keyset
pagination; unknown parameters, invalid cursors, cross-branch access and foreign
device actors remain bounded. Native CI passes structure and architecture for 66
projects, secrets, dependencies, formatting, migration/restore, 167 unit/configuration/
HTTP tests and 71 integration tests with zero failures or skips. Next define the
strict offline-sale payload contract and atomically materialize it through the
existing Sales, Payments, Inventory and outbox transaction with durable result evidence.

## September 10, 2026 — ordered versioned sync ingestion

Implemented Sync as five module projects and added an authorized server ingestion
boundary for protocol-v1 `sale.completed.v1` envelopes. Each registered active
device receives an immutable, strictly monotonic sequence. A transaction-scoped
per-device lock prevents concurrent gaps; duplicate message IDs replay only when
device, sequence, protocol, type and the server-computed SHA-256 payload digest
match exactly. Changed replay, gaps, stale sequences, unsupported versions and
unregistered devices are rejected without advancing the durable checkpoint.

Migration 028 adds forced-RLS immutable message evidence, tenant/device sequence
uniqueness and insert/select-only runtime grants. `sync.ingest` is checked through
active hierarchy and membership scope for ingestion and checkpoint reads. Native CI
passes structure and architecture for 66 projects, secrets, dependencies, formatting,
migration/restore, 167 unit/configuration/HTTP tests and 71 integration tests with
zero failures or skips. Next validate and atomically materialize the accepted offline
sale envelope into the existing Sales, Payments, Inventory and outbox records while
preserving the local sale ID, shift/register assignment and exact replay semantics.

## September 10, 2026 — durable registered-device identity

Implemented Devices as five module projects and established the durable device
identity required by offline synchronization. Authorized operators can register,
retrieve and list active desktop, mobile and kiosk devices bound to an existing
tenant branch register. Registration snapshots the supported sync protocol version,
database time and authenticated operator; operation-ID replay is stable and changed
replay or an unsupported protocol is rejected.

Migration 027 adds tenant-composite register ownership, unique branch device codes,
forced RLS, bounded cursor access and insert/select-only runtime privileges. Reads
require `devices.view`; writes require `devices.manage`, both after active hierarchy
and membership-scope validation. Native CI passes structure and architecture for 61
projects, secrets, dependencies, formatting, migration/restore, 167 unit/configuration/
HTTP tests and 71 integration tests with zero failures or skips. Next implement the
versioned Sync ingestion envelope with monotonic per-device sequence admission,
payload-digest replay validation and durable acknowledgement checkpoints.

## September 10, 2026 — authorized closed-shift reconciliation reads

Added authorized detail and bounded history endpoints for closed shifts. Both paths
require active hierarchy, membership scope and persisted `shifts.view` authority,
execute under forced tenant RLS and expose the immutable closing reconciliation:
opening cash, sales, refunds, cash-in/out, expected cash, counted cash and variance.
History uses strict 1–100 page sizing, UUID keyset pagination and a branch-scoped
partial index from migration 026. Unknown query parameters and malformed cursors
fail with a bounded 400 response; cross-branch detail remains undiscoverable.

Native CI passes all structure, architecture, secret, dependency, formatting and
migration/restore checks, 167 unit/configuration/HTTP tests and 70 integration tests
with zero failures or skips. Next begin the offline client transaction and
synchronization foundation: durable device identity, monotonic client operations,
idempotent server admission and an authorized sync boundary before building local
sale capture.

## September 10, 2026 — shift reconciliation and authoritative closing

Returns and sale voids now copy the original completed sale's persisted shift and
register assignment into their immutable records and generated cash-refund evidence.
Their admission shares the shift transaction lock with sales, cash movements and
closing and requires that shift to remain open, preventing a refund from racing past
the closing snapshot.

Migration 025 adds tenant-composite evidence links and immutable closing totals.
Authorized `shifts.close` writes one idempotent close transition containing opening
cash, cash sales, return/void refunds, cash-in, cash-out, expected cash, counted cash
and exact variance. Changed replay is rejected and a closed shift admits no later
sale, cash movement, return or void. Native CI passes all structure, architecture,
secret, dependency, formatting and migration/restore checks, 167 unit/configuration/
HTTP tests and 70 integration tests with zero failures or skips. Next add bounded
closed-shift history/detail and cashier-facing reconciliation projections, then begin
the offline client transaction and synchronization foundation.

## September 9, 2026 — cash sales assigned to active shifts

Cash-sale completion now requires an explicit shift and atomically snapshots its
register assignment on the completed sale. The server validates that the shift is
open in the requested tenant branch, derives the register and currency from persisted
shift state, rejects currency mismatch, and serializes sale admission with shift cash
operations. Idempotent replay now includes the shift identity in request equivalence.

Migration 024 adds nullable transition columns for historical sales, tenant-composite
foreign keys to shifts and registers, and a filtered shift/time access path. The active
API never creates an unassigned sale. Sale detail and history expose assignment while
remaining able to represent pre-migration records. Native CI passes all structure,
architecture, secret, dependency, formatting and migration/restore checks, 167
unit/configuration/HTTP tests and 70 integration tests with zero failures or skips.
Next propagate the persisted shift assignment through return and void refund evidence,
then implement authoritative expected-cash calculation and shift closing.

## September 9, 2026 — immutable shift cash-movement ledger

Added authorized cash-in and cash-out recording and retrieval beneath an open shift.
Each movement snapshots its shift currency, exact positive amount, bounded reason,
database timestamp and authenticated operator. Operation-ID replay is stable, changed
replay is rejected, and a transaction advisory lock serializes movement admission
against the owning shift.

Migration 023 adds tenant-composite shift references, forced RLS, an ordered lookup
index and insert/select-only runtime privileges, leaving update and delete unavailable.
Writes require persisted `shifts.cash.manage`; reads require `shifts.view`, both after
active hierarchy and membership-scope checks. Native CI passes all structure,
architecture, secret, dependency, formatting and migration/restore checks, 167
unit/configuration/HTTP tests and 70 integration tests with zero failures or skips.
Next bind sale and refund evidence to the active register shift before implementing
authoritative expected-cash calculation and closing variance.

## September 9, 2026 — authorized shift-opening slice

Implemented ShiftManagement as five module projects with versioned endpoints to open
and retrieve the current register shift. Opening snapshots the register, currency,
opening cash balance, database timestamp and authenticated operator. Operation-ID
replay returns the original shift, changed replay is rejected, and a transaction
advisory lock plus a PostgreSQL partial unique index enforce at most one open shift
per tenant register under concurrency.

Migration 022 establishes the tenant-composite register reference, forced RLS and
insert/select-only runtime grants. Both paths validate the active organization,
business, branch, membership scope and persisted `shifts.open` or `shifts.view`
authority before accessing shift data. Native CI passes structure, architecture
coverage for 56 projects, secrets, dependencies, formatting, migration/restore,
167 unit/configuration/HTTP tests and 70 integration tests with zero failures or
skips. Next add immutable cash drawer movements and authorized shift closing with
expected-versus-actual balance and variance evidence.

## September 9, 2026 — tenant-safe register catalog slice

Implemented Stores as five module projects and established persisted register identity
as the prerequisite for shift and cash-drawer workflows. Authorized operators can
create and list branch registers through versioned endpoints. Register codes are
bounded and unique within a tenant branch, names preserve user text, and creation is
operation-ID idempotent with changed replays rejected.

Migration 021 adds tenant-composite branch references, forced RLS, a branch/cursor
index and insert/select-only runtime grants; runtime update and delete are denied.
Creation requires persisted `stores.manage`, while listing requires `stores.view`,
with active organization/business/branch and membership scope checked before data
access. Native CI passes structure, architecture coverage for 51 projects, secrets,
dependencies, formatting, migration/restore, 167 unit/configuration/HTTP tests and
69 integration tests with zero failures or skips. Next implement ShiftManagement's
authorized, idempotent shift-opening workflow with one open shift per register.

## September 9, 2026 — unified branch payment-event history

Added a bounded, branch-scoped reconciliation feed that projects completed cash
captures, return refunds and sale-void refunds into one versioned Payments API.
Every event retains its source kind and identifier, original payment identifier,
currency, exact amount, status and database completion time. A URL-safe opaque cursor
encodes the complete `(completed_at, kind, id)` ordering key so heterogeneous events
with equal timestamps remain stable across pages. Unknown parameters, malformed
cursors and page sizes outside 1–100 fail with a bounded 400 response.

Migration 020 adds branch/time access-path indexes for all three persisted sources.
Reads require active hierarchy, membership and persisted `payments.view` authority
and remain protected by each source table's forced tenant RLS. Native CI passes all
structure, architecture, secret, dependency, formatting and migration/restore checks,
167 unit/configuration/HTTP tests and 68 integration tests with zero failures or skips.
Next establish register identity and an authorized shift-opening boundary as the
prerequisite for cash drawer reconciliation.

## September 9, 2026 — authorized refund payment retrieval

Extended the Payments boundary with immutable refund projections for both partial/full
returns and completed-sale voids. Authorized callers can retrieve the cash refund by
its owning return or void within an explicit organization and branch scope. Responses
preserve the original payment identifier, source kind and identifier, method, status,
currency, exact amount and database completion timestamp without exposing provider or
cardholder data.

Both query paths require persisted `payments.view` authority after active hierarchy
and membership checks and execute under the existing forced tenant RLS policies.
Integration coverage verifies the exact return and void refund projections. Native CI
passes all structure, architecture, secret, dependency, migration/restore and formatting
checks, 167 unit/configuration/HTTP tests and 68 integration tests with zero failures or
skips. Next add a bounded branch payment-event history that consistently represents
captures, return refunds and void refunds for reconciliation consumers.

## September 9, 2026 — authorized sale-void retrieval and reversal exclusion

Added branch-scoped retrieval of one immutable sale void and bounded void history
with UUID keyset pagination and a strict 100-item maximum. Both reads require the
persisted `sales.void` authority after active hierarchy and membership checks and
execute under forced tenant RLS. Migration 019 adds the tenant/branch/cursor index.

Sale void and return completion now share one transaction-scoped per-sale reversal
lock. Return completion checks for a persisted void inside that serialized boundary,
so a voided sale cannot subsequently be returned and concurrent void/return attempts
cannot both commit. Native CI passes all structure, architecture, secret, dependency,
migration/restore and formatting checks, 167 unit/configuration/HTTP tests and 68
integration tests with zero failures or skips. Next expose the immutable refund
records produced by return and void workflows through authorized payment queries.

## September 9, 2026 — atomic completed-sale void slice

Implemented controlled voiding of completed cash sales under persisted `sales.void`
authority. A transaction-scoped sale lock prevents concurrent reversal. A sale with
any return is rejected, and one sale can have only one immutable void. The same
transaction writes the void and line evidence, restores each sold quantity through
inventory movements and creates a cash void-refund linked to the original payment.
Operation-ID replay returns the original result without duplicate effects.

Migration 018 adds forced-RLS sale voids, line evidence and payment void-refunds with
minimum runtime grants and database-triggered refund capture. Focused native PostgreSQL
verification passes 167 unit/configuration/HTTP tests and 68 integration tests,
including exact stock restoration, one refund, replay and duplicate-void rejection.
Next add authorized void detail/history reads and exclude voided sales from return
eligibility.
## September 9, 2026 — atomic suspended-cart completion slice

Cash-sale completion can now consume an owner-bound suspended cart in the same
database transaction that writes the immutable sale, payment, inventory movements
and outbox event. The supplied cart must be active, unexpired, owned by the caller
and have exactly the same product quantities as the completion request. A cart is
linked to one completed sale and cannot be consumed again. Failed pricing, stock or
payment work rolls back the cart transition with every other write.

Migration 017 adds the tenant-composite completed-sale link and restricts runtime
mutation to the state-transition columns. Operation-ID sale replay verifies the
persisted cart association. Focused native PostgreSQL verification passes 167 unit/
configuration/HTTP tests and 67 integration tests, including the persisted sale link
and second-consumption rejection. Next implement controlled sale voids before payment
settlement boundaries expand.
## September 9, 2026 — owner-bound suspended cart slice

Implemented suspended sale carts in the existing Sales module. An authorized cashier
can persist a bounded product/quantity snapshot without reserving stock or creating
financial records. Suspensions use operation-ID replay, retain an optional bounded
note and expire exactly 24 hours after database time. Resume is serialized per cart,
requires the same organization, branch, issuer and subject, and succeeds only once
before expiry. PostgreSQL also enforces the permitted state transition and prevents
the runtime role from deleting carts or changing their immutable fields.

Migration 016 adds forced-RLS cart headers and lines, tenant-composite references,
owner/expiry lookup and minimum column grants. Focused native PostgreSQL verification
passes 167 unit/configuration/HTTP tests and 67 integration tests, including owner
isolation and one-time resume. Next connect resumed carts directly to atomic sale
completion so consuming a cart and completing its payment cannot diverge.
## September 9, 2026 — authorized return retrieval and history slice

Added branch-scoped retrieval of one immutable return and a bounded return-history
endpoint with UUID keyset pagination and a strict 100-item maximum. Both operations
require active hierarchy, membership and persisted `sales.refund` authority before
reading tenant-isolated return data. Unknown records return 404 only after authority
is established; malformed or unknown query inputs return a bounded 400 response.
Migration 015 adds the tenant/branch/cursor index used by history reads.

Focused native PostgreSQL verification passes 167 unit/configuration/HTTP tests and
66 integration tests. Coverage now reads returned line quantities, exercises bounded
history, rejects invalid page sizes and denies unauthorized history access. Next
implement suspended sale carts with explicit expiry and safe resume ownership.
## September 9, 2026 — quantity-bounded partial-return slice

Extended the atomic Returns workflow to accept an explicit bounded set of product
quantities. Multiple returns can now reference one sale, while a transaction-scoped
sale lock and persisted prior-return totals prevent cumulative quantity or money
from exceeding each immutable sale-line snapshot. Intermediate refund amounts use
the shared six-decimal half-even policy; the final remaining quantity receives the
exact unrefunded balance so split returns reconcile to the original gross amount.

Migration 014 removes the former one-return-per-sale restriction and adds the
indexes and per-return product uniqueness needed for safe cumulative checks. Each
partial return still records its refund, stock restoration and immutable return
lines in one transaction. Operation-ID replay now compares the requested product
set and quantities before returning an existing result. Native PostgreSQL coverage
passes 167 unit/configuration/HTTP tests and 66 integration tests, including two
successive partial returns and rejection beyond sold quantity. Next add authorized,
bounded return history and individual return retrieval from the immutable records.
## September 9, 2026 — atomic full-sale return slice

Implemented Returns as five module projects in the supplied Returns structure.
The first versioned workflow performs a full return against an original completed
cash sale. It requires persisted branch-scoped `sales.refund` authority, locks the
sale, rejects a second return, and supports safe operation-ID replay.

Migration 013 adds immutable return headers and line snapshots with forced tenant
RLS. One transaction records the return, restores every sold quantity through
positive inventory `return` movements and creates one completed cash refund linked
to the original payment. Runtime access cannot update or delete returns/refunds or
insert refunds directly. Focused native PostgreSQL verification passes 167 unit/
configuration/HTTP tests and 66 integration tests. Coverage includes stock restoration,
one refund, replay without duplicate effects, authorization and database isolation.
Next extend this boundary with quantity-bounded partial returns.

## September 9, 2026 — bounded sale-history query slice

Added a branch-scoped sale-history endpoint with a strict maximum page size of 100
and stable UUID keyset pagination. The query requires `sales.view`, repeats active
hierarchy and membership authorization, executes under forced tenant RLS and returns
only immutable completed-sale summaries. Unknown, duplicate or malformed query
parameters fail with a bounded 400 response. Migration 012 adds the composite index
used by the tenant, branch and cursor access path.

Focused PostgreSQL coverage verifies the migration index, list authorization,
bounded input and persisted sale visibility. Integration coverage walks consecutive
one-item pages and verifies that the cursor cannot repeat a row. Next implement the
Returns boundary with immutable return records and atomic reversing stock movements.

## September 9, 2026 — atomic cash-payment association slice

Implemented Payments as five module projects in the supplied Payments structure.
Migration 011 adds immutable, tenant-isolated payment records and an internal
database trigger that creates exactly one completed cash payment in the same
transaction as each completed sale. The payment preserves the sale, branch,
method, status, currency, amount, tendered cash, change and completion timestamp.
The runtime role can read payment records but cannot create, update or delete them.

The versioned payment query is scoped by organization, branch and sale and requires
the persisted `payments.view` permission. It returns no cardholder or provider
secret data. Database checks verify atomic capture, exact financial snapshots,
immutability, denied direct runtime insertion and tenant isolation. Application
coverage verifies the payment response created by the existing cash-sale flow,
including permission denial. The focused native PostgreSQL run passes 167 unit/
configuration/HTTP tests and 64 integration tests with zero failures or skips.
Next implement sale listing with bounded keyset pagination, then sale returns and
their reversing inventory/payment records.

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
