# SalekhPos technology and architecture

Original design: September 6, 2026. English edition: September 7.
This document preserves the original design rationale. Current implementation
status is in ../development/progress.md; later accepted changes are recorded in
../adr/001-master-architecture.md and master-implementation-plan.md.

Product owner: Salekh. Development is undertaken by Salekh with AI assistance,
without a separate development/operations team. Georgia is the probable initial
market. Store type, device models and hosting budget were not supplied.
The source directory is the user's SalekhPos project under Documents/PROJECTS;
the repository is [AlakhiarovSalekh/SalekhPos](https://github.com/AlakhiarovSalekh/SalekhPos).
It was public and empty when initially inspected. This design is not a completed
ERD, set of screen prototypes, API specification or compliance assessment.

## 1. Technology direction

Use C#, ASP.NET Core/.NET 10 LTS and PostgreSQL for a modular backend. Native clients
use Flutter/Dart with SQLite and an explicit synchronization protocol. The initial
proposal used adaptive Flutter for web as well; the subsequently supplied master
specification selects React/TypeScript web. No working client was replaced.

The rationale is shared functionality across native platforms, centralized server
integrity and a manageable maintenance burden. Each OS still requires native
integration, separate builds and actual device tests.

| Component | Original candidate | Decision limitation |
|---|---|---|
| Runtime/backend | .NET 10 LTS, ASP.NET Core, modular monolith | API and workers share modules and release lineage |
| Main database | PostgreSQL 18, supported provider patch; 17 fallback if needed | Provider/version/region must be verified |
| Data access | EF Core 10/Npgsql 10; parameterized SQL where appropriate | Current bounded reads use Npgsql without unnecessary ORM |
| Native client | Stable Flutter and bundled Dart | Prove all target platforms and hardware |
| State/navigation | Riverpod, go_router | Validate in a small vertical slice |
| Local persistence | Drift/SQLite | Native durability and browser persistence are distinct |
| Local encryption | SQLCipher and OS key storage | Build, licensing, migration, recovery and rotation tests required |
| API | HTTPS REST/JSON and OpenAPI | Exact money/date/error contracts required |
| Identity | OIDC; native authorization code + PKCE; web BFF session | Provider remains open |
| Background work | PostgreSQL outbox/job tables and .NET Worker | No broker required initially |
| Files | Private managed object storage with adapters | Cloud provider remains open |
| Observability | OpenTelemetry, structured logs, managed monitoring | Deployment/exporters remain open |
| Tests | xUnit, PostgreSQL/Testcontainers, Flutter tests, k6 | Pin actual packages; real DB tests cannot use an in-memory substitute |
| Packaging | OCI containers and local Compose-compatible environment | Runtime/licensing and operational needs determine setup |
| CI/CD | GitHub Actions with Windows/Linux/macOS jobs as needed | Signing accounts and budget remain open |
| Hosting | Managed containers and PostgreSQL | Azure was a reference candidate, not a purchase decision |

At the original assessment, Microsoft listed .NET 10 LTS support through November 14,
2028 and .NET 8 support through November 10, 2026. PostgreSQL 18 support was listed
through November 14, 2030. Recheck lifecycle and provider availability before
changing production dependencies.
[.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy),
[PostgreSQL version policy](https://www.postgresql.org/support/versioning/),
[Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html).

## 2. Alternatives and tradeoffs

| Client option | Benefit | Cost/risk and disposition |
|---|---|---|
| Flutter/Dart | Shared native UI/workflows across five OS targets and possible web | Hardware plugins, accessibility and complex tables need proof; native candidate |
| Avalonia/C# | Same language as backend; strong desktop orientation | Verify mobile/browser support tiers; alternative if C# becomes decisive |
| Uno/C# | Windows/Linux/macOS/Android/iOS/WebAssembly direction | Hardware and build-flow proof required |
| Tauri/web UI | Share web functionality in a native shell | Rust/platform plugins add maintenance; reconsider for web-led strategy |
| Electron plus mobile solution | Share desktop/web code | Separate Android/iOS solution; not the initial choice |
| .NET MAUI | C# ecosystem | No official primary Linux desktop target; web separate, so not chosen |

These are qualitative requirement-fit judgments, not fabricated benchmarks.
[Flutter platforms](https://docs.flutter.dev/reference/supported-platforms),
[Avalonia](https://docs.avaloniaui.net/docs/supported-platforms),
[Uno](https://platform.uno/docs/articles/getting-started/requirements.html),
[Tauri](https://v2.tauri.app/develop/plugins/),
[Electron](https://www.electronjs.org/docs/latest/),
[MAUI](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0).

The original Flutter Web proposal required table, keyboard, screen-reader and startup
performance tests; failure would reopen a DOM-based frontend decision. The later
master specification resolves web toward React. A public marketing site is separate.

.NET is not assumed automatically faster or safer than all alternatives. Typed
domain models, support policy and transactional integration fit this project.
Java/Spring and TypeScript backends remain possible but are not required.
PostgreSQL is authoritative; SQLite is local. Avoid premature document databases,
search servers and analytical stores. ORM query filters alone are not security.

## 3. System architecture

Native Flutter → local durable database/outbox and hardware adapters → ASP.NET API.
Web management → BFF/session boundary → the same API.
Native/BFF identity flows use an OIDC provider. Modules use PostgreSQL and private
object storage; workers process reports, imports, notifications, reconciliation
and outbox events. API/workers emit safe observability data.

API and worker share the repository, domain rules and release lineage. Modules
communicate through application services and versioned contracts, not uncontrolled
writes into each other's tables. A cash-sale transaction atomically coordinates
sale, cash, stock and outbox effects in PostgreSQL.

Planned modules include identity/access, organizations/businesses/regions/branches,
catalog, pricing/tax, sales, payments, cash/shifts, inventory/warehousing, procurement/
suppliers, customers, reporting, devices/sync, audit and integrations. Loyalty,
promotions, subscriptions and regional administration remain part of the master
scope. Payroll and complete statutory accounting require separate domain/legal design.

Do not add Kubernetes, service meshes, Kafka, Redis, Elasticsearch, microservices
or universal event sourcing merely for appearance. Durable ledgers are required
without making every entity event-sourced. Extract services only for measured needs.

## 4. Organization and access model

The original minimal organization→branch model is superseded by explicit
organization→business→optional region→branch. Warehouses need not sit inside stores.
Users and role assignments are relationships, not leaves limited to one branch.
One person may have membership in multiple organizations or businesses.

| Entity | Responsibility |
|---|---|
| User identity | Provider issuer + subject |
| Membership | Organization association, active state and validity |
| Role/permission | Default templates plus configurable independent permissions |
| Assignment | Membership, role and organization/business/region/branch/warehouse scope |
| Approval policy | Amount, operation, reason and second-approver rules |
| Device/register | Trusted physical installation versus logical register |
| Entitlement | Organization feature availability, never a substitute for permission |

Platform administration does not imply silent tenant access. Support needs explicit
scope, reason, duration and audit. Founder/Owner emergency access must be recorded.

Tenant tables use non-null organization IDs and composite FKs; barcode/document
uniqueness is scoped appropriately. Every request checks validated membership and
scope. Runtime is not a table owner, superuser or BYPASSRLS role; transactions contain
tenant context and pool reuse must not leak it. RLS is defense in depth, not a cure
for injection or stolen credentials.
[PostgreSQL RLS](https://www.postgresql.org/docs/18/ddl-rowsecurity.html).

## 5. Money, products and inventory

Use decimal on the server, suitable numeric database columns, and exact decimal/
scaled-integer client representation. API money/quantities are decimal strings.
Shared JSON fixtures must compare C# and client arithmetic.

Currency is explicit; do not assume two fractional digits. Unit-price precision,
quantity precision, tax rounding and settlement rounding are different policies.
Validate supported currencies and limits in versioned country/business configuration.
Cross-currency consolidation requires historical rates, dates and sources.

Product structures include variants/SKUs, multiple textual barcodes preserving leading
zeros, units/conversions, categories and translations. Batch/expiry/serial tracking
is optional by product. Sale lines preserve historical names, units, prices,
tax-policy versions, discounts and totals.

StockMovement is the traceable ledger; StockBalance may be an atomic projection.
No arbitrary balance editing: counts create approved adjustment movements.
Moving weighted-average costing was a candidate; FEFO may govern physical issue.
Costing and picking are distinct. Backdated receipt/cost policies need pilot decisions.

Online concurrent sales use atomic stock controls or reservations. Offline terminals
cannot guarantee globally current stock. Define offline allowances, warnings and
reconciliation; high-value, scarce or serialized goods may need restrictions.

## 6. Sale, payment and refund consistency

| Entity | Candidate states |
|---|---|
| Sale | Draft, Held, AwaitingPayment, Completed, Cancelled |
| Payment attempt | Created, Pending, Succeeded, Failed, Unknown |
| Refund | Requested, Pending, Succeeded, Failed, Unknown |
| Fiscal document | NotRequired, Pending, Issued, Failed, NeedsReview |
| Print job | Queued, Sending, Printed, Failed, Unknown |

A printer failure is not necessarily a failed sale. Completed sales are not deleted
or changed back to drafts. Returns/refunds reference original documents; cumulative
and concurrent returns cannot exceed sold quantities. Goods receipt and refund are
different events. Returned damaged goods do not become available stock automatically.

A cash sale atomically persists lines, cash ledger, stock movements and outbox.
External card providers do not share a database transaction: persist an attempt,
call with a stable idempotency identity, reconcile, then persist local effects.

Lost responses must not lead to a new blind charge. Webhooks/status queries converge
on one attempt; duplicate callbacks cannot apply effects twice. A captured payment
with incomplete sale goes to reconciliation/review. Automatic refunds require an
explicit policy. Split tenders preserve each part's state, currency, merchant and
remaining balance. Provider idempotency retention must cover the retry strategy.

## 7. Offline and synchronization

Local sale and outbox commit together before “saved locally” is shown. Server
acknowledgement is a separate state. Cache only the authorized catalog/configuration
and minimum necessary personal data.

The planned envelope includes organization, branch, device, operation ID, local
sequence, schema version, pricing/tax version, occurred-at, payload and hash.
A device identity or signature does not prove business correctness; the server validates.

Use at-least-once delivery with durable deduplication scoped by organization/device/
operation. Same key with different payload is a conflict. Planned responses are
Accepted, AlreadyApplied, NeedsReview and Rejected with stable codes. Local sync
completion requires durable server acknowledgement.

Downloads use a versioned change feed and cursor, including tombstones. Do not rely
only on wall-clock comparison. Expired cursors require a fresh snapshot while
preserving unsent local operations. Specify chunks, compression, retry/backoff and
429/503 behavior. Never use universal last-write-wins for money, stock or sales.
Old offline prices remain historical facts subject to explicit acceptance/review.

Offline identity has a bounded lease. Revocation cannot reach a disconnected device
instantly. Local cashier PIN is not a replacement for server authentication or MFA.
Device registration, retry limits and offline duration policies remain required.
Mobile background execution is not guaranteed; restart must safely resume work.

Drift's browser persistence differs from native behavior, including eviction,
multiple tabs and backend support. Web starts online-first.
[Drift platforms](https://drift.simonbinder.eu/platforms/),
[web limitations](https://drift.simonbinder.eu/platforms/web/).
SQLCipher is a candidate, not the default SQLite build. Verify encryption, WAL,
backup, migration and key rotation on each OS.
[SQLCipher](https://www.zetetic.net/sqlcipher/).

## 8. Hardware and country adapters

Define scanner, receipt printer, cash drawer, scale, customer display, label printer,
payment terminal and fiscal-provider interfaces. Package platform badges are not
acceptance evidence. Record model, firmware, transport, OS, driver/SDK and tested
operations. Test paper-out, disconnect, duplicate/uncertain print, glyphs, QR/cutter,
scale units/tare, malformed readings, terminal timeout/cancel/refund and duplicate callbacks.

If browser hardware access is insufficient, a signed local connector may be needed.
It must use loopback, origin allowlists, pairing/authentication and a narrow API;
arbitrary websites must not gain shell/device access. Build it only if actual hardware requires it.

Country packages own effective-dated tax, rounding, fiscal numbering, receipt
templates, provider availability and retention. Georgia is a pilot candidate,
not a hardcoded core rule or compliance claim. Each market needs current official
research and local verification.

The original Georgian pilot proposed ka-GE, GEL and Asia/Tbilisi as configurable
pilot defaults, with ka/az/en language packs and personal language preference.
Test Georgian glyphs, currency symbols and mixed languages on actual printers.
Never assign one tax rate to every product.

The original research identified the Revenue Service fiscal-register information and
TBC's QR API. Neither establishes SalekhPos fiscal certification or physical card
terminal SDK access. Resolve ECR protocol, platform SDKs, status/refund and sandbox
with the selected vendors; no provider was selected or contacted.
[Revenue Service](https://www.rs.ge/CashRegister?cat=1&tab=1),
[TBC QR API](https://developers.tbcbank.ge/docs/create-qr-payment).

## 9. Localization, time and experience

Native localization should use resources such as ARB/generated localization, with
pluralization, placeholders, RTL and fallback tests. Product translations are data.
[Flutter internationalization](https://docs.flutter.dev/ui/internationalization).

Language is independent of country. Preserve branch IANA timezone, UTC event time
and business-day semantics. Test DST and overnight shifts. Receipt and employee
languages may differ. Late offline operations after period close need explicit policy.

Use role-appropriate navigation, keyboard shortcuts, large touch targets, status
indicators beyond color, readable contrast and screen-reader semantics.
[Accessibility](https://docs.flutter.dev/ui/accessibility).
Planned screens cover sign-in/organization selection, setup, device registration,
shifts, sale/payment/receipt, returns, catalog, receiving/counts, purchasing, customers,
reports, employees/permissions, sync issues, audit and settings. Platform administration
has a separate privileged boundary. Small stores need not configure departments,
regions or elaborate approvals. Training mode must isolate effects from real stock,
reports and terminals; production does not contain fake business data.

## 10. API and compatibility

Planned /api/v1 resources include organizations, memberships, branches, devices,
catalog, price books, shifts, sales, payments, returns, receipts, inventory,
purchase orders, goods receipts, reports, sync and audit. The current branch-only
OpenAPI is not a contract for all of them.

Writes need idempotency, mutable resources need optimistic concurrency, lists need
bounded cursors, and errors need stable codes and correlation IDs. Do not derive
program behavior from translated messages. IDs are opaque; money is exact text.

Generated client selection requires nullability/decimal/date/error fixture testing.
The backend is authoritative; offline calculations use the same versioned policies.
Define minimum supported client/protocol versions and grace windows. Security updates
must consider recovery of unsent sales. Evolve contracts through compatible expansion,
migration and later cleanup rather than instantly breaking older native clients.

## 11. Security

Use an ASVS 5.0-oriented web/API checklist and MASVS mobile categories. ASVS Level 2
plus selected stronger controls was an initial target, not certification.
[ASVS](https://owasp.org/www-project-application-security-verification-standard/),
[MASVS](https://mas.owasp.org/MASVS/).

Native OIDC uses the system browser, authorization code and PKCE, never an embedded
client secret. Test desktop loopback redirects and mobile verified links.
[RFC 8252](https://datatracker.ietf.org/doc/html/rfc8252).
The web BFF keeps tokens server-side and uses secure HttpOnly sessions with CSRF,
Origin and SameSite controls. Native refresh tokens belong in OS secret storage.
Verify issuer/audience/signature/lifetime/membership, logout and device revocation.

A managed provider was preferred. Keycloak is a local/self-hosted alternative with
its own patching, HA, backup and account-recovery burden. Provider selection depends
on budget, region, MFA and recovery requirements.
[Keycloak](https://www.keycloak.org/securing-apps/oidc-layers).

Threats include stolen terminals, malicious cashiers, cross-tenant access, forged
webhooks, replay, hostile imports, supply-chain attacks and operator mistakes.
Keep secrets out of source/logs, isolate database networking, validate file sizes/types,
authorize personal-data exports and prevent CSV formula injection.

Audit records actor, device, tenant, scope, action, reason, result, time and correlation.
Financial, permission, export and support actions need durable audit. Append-only
runtime grants and separately protected archives are candidates; ordinary tables
are not absolutely immutable to the infrastructure administrator.

Card data should remain with the terminal/provider. Never retain prohibited payment
authentication secrets. Tokenization does not automatically eliminate PCI scope.
[PCI SSC guidance](https://www.pcisecuritystandards.org/faqs/1533/).

## 12. Hosting, growth and operations

Begin with managed API/worker and PostgreSQL in one region. Production requires
zone redundancy, managed secrets/keys, private networking, TLS, backups, monitoring
and budget controls. Local Compose is not production HA.

Azure Container Apps and Azure Database for PostgreSQL were reference candidates.
Region-specific support, version, capacity and cost require verification before
provisioning; this document authorizes no purchase.
[Container Apps zones](https://learn.microsoft.com/en-us/azure/container-apps/how-to-zone-redundancy),
[PostgreSQL HA](https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-high-availability).

Do not begin with global active-active writes. Tenant home-region design permits
later regional cells, but a region field alone does not solve residency or latency.
Backups, replicas and logs also obey residency policies.

Optimize schemas/queries and API replicas before adding reporting replicas and
tenant distribution. Total connection pools across replicas must fit DB limits.
Quotas and worker fairness prevent a large tenant/import from starving checkout.
Track latency, errors, DB locks/pools, sync lag, duplicates, uncertain payments,
print failures, job age and backup results without sensitive payloads.
[OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/instrumentation/).

Replication is not a backup: accidental deletions replicate too. Restore PITR into
a separate environment and verify business invariants.
[PostgreSQL restoration](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore).

## 13. Repository and releases

Use apps/web and apps/client for future clients, server/src for API/workers/modules,
server/tests for backend tests, packages/contracts for actual shared contracts,
infra for migrations/deployment and docs for decisions/runbooks. Do not create empty
folders to imitate a diagram. Pin SDK/packages/actions/images; scan dependencies
and secrets, preserve SBOM/license evidence, and verify signatures and provenance.
A public repository is not an implicit software license; the owner chooses licensing.

Native release builds need their respective environments: macOS for Apple targets,
Windows for Windows, Linux for Linux. Plan signing accounts, macOS CI and actual
devices; one Windows machine does not prove all six release targets.
[Platform integration](https://docs.flutter.dev/platform-integration).

The user's OneDrive source location remains. Git is version history; live PostgreSQL/
SQLite databases, credentials, production records and caches remain outside it.
Do not edit the same working tree concurrently from two computers. Revisit the path
if OneDrive conflicts occur.

## 14. Companion plan and evidence limits

[Acceptance and delivery plan](acceptance-and-delivery-plan.md) retains concrete
security, load, offline and recovery cases. The initial work consisted of source/
repository inspection and official-reference design research, not implemented
platform support, legal compliance or full dependency-license review. Current
progress and ADRs record later verified work separately.
