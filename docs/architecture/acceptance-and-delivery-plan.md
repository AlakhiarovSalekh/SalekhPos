# SalekhPos acceptance and delivery plan

Date: September 6, 2026. Updated and translated September 7, 2026. Status: active plan; each implemented result is tracked separately in `../development/progress.md`.

Primary document: [Technology and architecture](technology-and-architecture.md).

Product scope: SalekhPos for small, medium, large, and multi-branch retailers; international expansion; selectable languages; Windows, Linux, macOS, Android, iOS, and web; strong security; and thousands of users. Salekh and AI will develop it. Georgia is the probable first market. Hardware models remain unknown.

## 1. Decision register

| ID | Decision | Status | Closure evidence |
|---|---|---|---|
| ADR-001 | .NET 10 backend, PostgreSQL, modular monolith | Accepted | Technical foundation review and automated boundary check |
| ADR-002 | Flutter for five native platforms; React for web | Conditionally accepted | SPIKE-01–04; web direction resolved by the master documents |
| ADR-003 | Native Drift/SQLite with encryption | Conditional | SPIKE-02 on each OS: keys, migration, recovery |
| ADR-004 | Web online-first; offline support has a separate gate | Accepted | Browser persistence, multiple-tab, and eviction tests |
| ADR-005 | Tenant isolation through application authorization and RLS | Accepted | SEC-01–04 |
| ADR-006 | Separate sale, payment, and fiscal states | Accepted | PAY-01–05 |
| ADR-007 | Durable outbox, unique operation identity, replay validation | Accepted | SYN-01–07 |
| ADR-008 | Managed OIDC in production with a provider-independent contract | Provider open | MFA, recovery, region, price, and session tests |
| ADR-009 | Managed cloud, growing in stages from one region | Provider and budget open | Region, cost, backup, and failover tests |
| ADR-010 | Georgia as first-market candidate | User direction | Store and fiscal-integration confirmation |
| ADR-011 | Hardware only through a certified compatibility list | Proposed rule | Model, firmware, and OS matrix |
| ADR-012 | Design first, then a narrow vertical slice | Accepted sequence | Design review |

Accepting a decision does not prove it. Every change records its reason, alternatives, impact, and evidence.

## 2. Meaning of platform support

A successful build alone does not make a platform supported. Sign-in, offline recording where promised, restart, update, localization, screen-reader behavior, and the promised hardware path must pass on that platform.

| Target | Initial validation line | Qualification |
|---|---|---|
| Windows | Windows 11 x64 | ARM64 and older systems require separate decisions |
| Linux | Selected Ubuntu LTS x64 | Support does not imply every Linux distribution |
| macOS | Supported macOS on Apple Silicon | Intel depends on the SDK lifecycle and a separate decision |
| Android | Real phone and tablet within the supported SDK range | Low-cost POS Android versions are an early filter |
| iOS/iPadOS | Real iPhone and iPad on supported OS versions | Requires macOS build and signing |
| Web | Chrome, Edge, Firefox, Safari | Online primary flow; per-browser storage and hardware matrix |

Minimum OS versions will be pinned when the Flutter stable version and first hardware are selected. This matrix is not certification.

## 3. Technical spikes before broad implementation

| ID | Prototype | Acceptance criterion | Response to failure |
|---|---|---|---|
| SPIKE-01 | Flutter register, large catalog, adaptive table | Keyboard and touch; 100,000 synthetic SKUs; search and large-text targets | Change UI composition and reopen native-client assumptions |
| SPIKE-02 | Encrypted native database and durable outbox | Sale and outbox survive restart/crash atomically; migration and key rotation pass | Change encryption binding or build choice |
| SPIKE-03 | Native OIDC and web BFF | Redirect, MFA, logout, CSRF, and token-leak tests on every OS | Change auth library or provider |
| SPIKE-04 | One printer and scanner, then payment terminal | Georgian receipt; paper-out, timeout, reprint, and status-query behavior | Change adapter/device and document platform limits |
| SPIKE-05 | Multi-tenant sale on .NET/PostgreSQL | RLS, concurrent stock decrement, rollback, idempotency | Correct domain and transaction boundaries |

Framework risk centers on SDK/hardware compatibility and offline data behavior. These spikes target those risks.

## 4. Initial domain and screen backlog

| Module | Data | Primary screen or operation | Critical exception |
|---|---|---|---|
| Organization | Organization, Business, Region, Branch, Warehouse | Setup and branch selection | Cross-tenant switch, closed branch |
| Access | Membership, Role, Scope, Approval | Staff and permissions | Self-elevation, removal of last Owner |
| Catalog | Product, Variant, Barcode, Unit | Product, import, search | Duplicate barcode, invalid unit conversion |
| Price/tax | PriceBook, RuleVersion | Prices and tax configuration | Stale price, rounding, effective date |
| Register | Register, Shift, CashEntry | Open, cash-in/out, close | Cash discrepancy, two open shifts |
| Sale | Sale, SaleLine, Discount | Cart, hold, checkout | Replay, insufficient stock |
| Payment | Attempt, Allocation, Refund | Payment and review | Unknown result, partial payment |
| Return | Return, ReturnLine | Receipt-based return | Excess quantity, original discount allocation |
| Inventory | Movement, Balance, Lot, Count | Receive, count, waste | Concurrent change, delayed sync |
| Procurement | Supplier, PO, Receipt | Order and partial receipt | Over/short/damaged goods, price variance |
| Transfer | Shipment, Receipt | Send and receive | In-transit stock, partial receipt |
| Customer | Customer, Contact, Consent | Customer selection | Excess personal data, duplicate |
| Reporting | Read models, Exports | Sales, stock, shift | Late operation, mixed currency |
| Device/sync | Device, Operation, Cursor | Devices and sync review | Old schema, revoked device |
| Audit | AuditEvent, SupportSession | Activity history | Export or support data leakage |

Before implementing a module, specify field types, uniqueness, foreign keys, indexes, API requests/responses, state transitions, and empty/loading/error/offline screen states. This table does not replace those details.

## 5. Security acceptance tests

| ID | Scenario | Expected result |
|---|---|---|
| SEC-01 | User of company A submits company B's ID | No data disclosure or mutation authority |
| SEC-02 | One pooled connection serves sequential A/B requests | Tenant context never crosses requests |
| SEC-03 | Worker, export, file URL, audit, or cache is called across tenants | Same scope enforcement as the API |
| SEC-04 | Runtime DB owner/bypass privileges and cross-tenant FK are tested | No bypass privilege; inconsistent relation rejected |
| SEC-05 | Cashier tries to change their role or discount limit | Server blocks and audits it |
| SEC-06 | Wrong issuer/audience, expired token, forged signature | Request rejected |
| SEC-07 | CSRF, session fixation, open redirect, post-logout replay | Session boundary remains protected |
| SEC-08 | Local database opened on another device or with wrong key | No plaintext access; key absent from code and logs |
| SEC-09 | Malicious/oversized import or export and formula payload | Safely rejected or neutralized |
| SEC-10 | Forged or repeated webhook | Provider rules validate status; no second effect |
| SEC-11 | Support access expires or is revoked | Access closes and audit remains |
| SEC-12 | Dependency and build-artifact validation | Known high/critical issues resolved; signature and provenance checked |

The threat model, risk register, and ASVS/MASVS requirements must map to test IDs. Scanner output and AI review do not replace independent security assessment. Budget a separate assessment before a live commercial release.

## 6. Money and inventory acceptance tests

| ID | Scenario | Expected result |
|---|---|---|
| PAY-01 | Checkout is submitted twice | One sale and one corresponding stock/money effect |
| PAY-02 | Terminal captures funds but response is lost | Unknown/Pending, reconciliation, no blind second charge |
| PAY-03 | Callback arrives before HTTP response | One payment result independent of arrival order |
| PAY-04 | Partial cash and card; card fails | Remaining balance visible; no false Completed state |
| PAY-05 | Two workers refund one sale concurrently | Refund limit cannot be exceeded |
| INV-01 | Two online registers sell the final unit | Atomic stock policy holds |
| INV-02 | Transfer sent and half received | Remainder stays in transit; no doubled stock |
| INV-03 | Crash during goods receipt | Receipt and stock commit together or neither commits |
| INV-04 | Receive by case and sell by unit | Exact conversion and balance |
| INV-05 | Price/tax changes and an old receipt is opened | Historical snapshot is immutable |
| INV-06 | Discounted item is partly returned | Refund follows the sold-line allocation |
| INV-07 | Reconcile sale and report | Totals match with auditable derivation |

Money and tax tests cover 0/2/3-decimal tender precision, large amounts, midpoint rounding, percentages, weight, discount allocation, and invalid input. C# and Dart must produce the same results from shared JSON cases.

## 7. Offline, crash, and recovery tests

| ID | Failure | Expected result |
|---|---|---|
| SYN-01 | Process stops while writing a local sale | Atomic record; no partial sale shown as successful |
| SYN-02 | Server accepts but acknowledgement is lost | Retry returns the prior result |
| SYN-03 | Same operation ID arrives with different payload | Conflict and audit; no silent overwrite |
| SYN-04 | Price or permission changes while offline | Versioned rule, time limit, review |
| SYN-05 | Cursor is too old | Fresh snapshot while preserving unsent sales |
| SYN-06 | Mobile OS kills the background app | Queue resumes after reopening |
| SYN-07 | 500 devices reconnect together | Backoff, fairness, and protection of online sales |
| REC-01 | App update occurs with pending sales | Migration loses no data |
| REC-02 | Bad deployment or DB migration | Pre-tested roll-forward or compatible rollback |
| REC-03 | DB failover or connection loss | Retry is safe; no duplicate sale |
| REC-04 | PITR restore into an isolated environment | Money/stock reconciliation and tenant checks pass |
| REC-05 | Printer prints but acknowledgement is lost | Unknown state and controlled reprint; no infinite retry |
| REC-06 | Disk fills or key is unavailable | No false success claiming the sale was saved |

Zero loss cannot be promised when an offline device is physically destroyed before upload. The pilot must decide acceptable risk, retention duration, and whether a second local copy is required.

## 8. Performance and reliability targets

These are initial test targets, not achieved results or a customer SLA. Record hardware, database size, region, network, app version, and workload with every result.

| Metric | Proposed initial target |
|---|---|
| Local barcode-to-cart | p95 <= 100 ms on reference hardware |
| Local catalog search | p95 <= 200 ms with 100,000 SKUs |
| Typical API read | server-side p95 <= 300 ms |
| Server sale transaction | excluding bank/fiscal waits: p95 <= 500 ms, p99 <= 1,500 ms |
| Technical request failure | < 0.1% under load; business rejections counted separately |
| Data integrity | zero lost/duplicate financial effects in tests; separate invariant checks |
| Primary API availability | initial production SLO 99.9% over 30 days; measurement boundary requires approval |
| Single-zone failure | RPO 0 target for writes confirmed by synchronous HA |
| Regional loss | initial DR target RPO <= 15 minutes, RTO <= 4 hours; confirm provider and budget |

At 99.9% over 30 days the unavailability budget is about 43.2 minutes. Include DNS, identity, and core API dependencies in the boundary; report bank/fiscal dependencies separately end to end. Local-sale continuity is a separate metric. A future 99.99% target requires a separate infrastructure and operations decision.

Ten thousand sessions do not mean ten thousand sales per second. If each of 10,000 registers completes one sale per minute, the result is about 167 sales per second. Load stages are 100, 1,000, and 10,000 active sessions. A planning example uses 200 sales/second, 10 lines/sale, and 800 additional reads/second until the business model is known.

The synthetic dataset contains 1,000 organizations, 10,000 registers total, 100,000 SKUs in a large tenant, and 10 million historical sale lines. Tenant sizes are uneven. Tests cover cold/warm cache, hot-SKU contention, 60 minutes sustained load, 2x peak, eight-hour soak, and 500 devices reconnecting with 100 pending sales each. Measure generator limits and use an arrival-rate model so system slowdown does not accidentally reduce offered load.

## 9. Test tools and CI

Use xUnit for backend domain tests and real PostgreSQL for RLS/transaction tests; an in-memory database is not a substitute. Flutter unit, widget, and integration tests cover UI and flows. Native permission dialogs and system UI require platform tooling or a device protocol.

Pull-request gates include formatting/analyzers, unit tests, critical DB integration tests, contracts, secrets, and dependency checks. Platform changes trigger matching OS builds/tests. Release gates cover all supported targets, signed artifacts, migrations, offline recovery, critical end-to-end flows, device matrix, load baseline, and review of known risks.

Coverage percentage is not the sole quality measure. Money, stock, and authorization invariants, negative scenarios, and real-device evidence matter. AI-authored code and tests require review; tests must validate behavior rather than repeat implementation.

## 10. Georgia pilot open issues

| Issue | Current knowledge | Closure path |
|---|---|---|
| First country | Probably Georgia | Confirm with first pilot customer |
| Store type | Unknown | Design a general retail base; confirm sector constraints |
| Language | Georgian required | Proposed initial `ka`, `az`, `en`; human review of Georgian |
| Fiscal device | Model unknown | Select through Revenue Service register and vendor SDK |
| Bank terminal | Model/bank unknown | Validate ECR protocol, platforms, refund/status, sandbox |
| Printer/scanner/scale | Unknown | Real hardware kit and compatibility test |
| Tax and receipt | Full compliance unverified | Current official rules plus local accountant/vendor confirmation |
| Legal entity/personal data | Unknown | Data map, retention, processing, hosting-region decision |
| Server budget | Not supplied | Resource estimate and monthly cap |
| Apple build/test | Availability unknown | macOS CI and access to a real iOS device |

The Revenue Service cash-register page confirms the fiscal-register regime but does not certify a SalekhPos integration. Mock adapters may support functional development before hardware testing. No mock result may be represented as a real integration.

## 11. Budget and operational ownership

Do not invent a monthly price. Estimate API/workers, primary and HA database, backups/archive, traffic, storage, identity, email/SMS, monitoring, macOS CI, signing, and developer accounts. One-time or periodic costs include the hardware kit, vendor integration, fiscal validation, and independent security testing.

A free/development tier is not evidence of production reliability. If budget cannot support HA, disclose the pilot boundary and risk; promise no unavailable SLA. Salekh is the human owner for incident notification. AI-assisted development does not create a 24/7 operations team.

## 12. Delivery sequence

| Phase | Result | Gate to next phase |
|---|---|---|
| A - Design | Stack, model boundaries, critical flows, open decisions | Review with Salekh |
| B - Technical risks | Five spikes and platform evidence | Close framework, DB, auth, hardware decisions |
| C - Core vertical slice | Organization -> cashier -> product -> cash sale -> stock -> receipt -> shift | Integrity and tenant tests |
| D - Complete store operation | Receiving, returns, procurement, counting, offline sync | Crash/recovery and reconciliation |
| E - Georgia pilot | Bank/fiscal adapter, translation, backup, device test | Real release checklist |
| F - Platform rollout | Accepted flows on all six platforms | Compatibility matrix and release artifacts |
| G - Growth | Multi-branch load, regional needs, additional countries | Load/DR and per-country acceptance |

All platforms remain in final-product scope. A narrow pilot does not remove other platforms from the architecture. During initial Windows/Linux/web work, verify iOS/macOS/Android builds and critical spikes early.

The first implementation package includes a domain ERD, Sale/Payment/Sync state diagrams, initial OpenAPI, permission matrix, screen wireframes, CI pipeline, local environment, and test fixtures. Building the full product means binding these artifacts to code step by step; every future country's detail cannot be known in advance.

## 13. Evidence status

Historical design work inspected the local workspace and GitHub metadata and researched platform and technology constraints. The current repository now contains a runnable backend foundation, CI, migrations, and automated tests; exact current evidence is maintained in [progress.md](../development/progress.md). Cloud deployment, production payment/fiscal integration, hardware certification, completed client applications, and measured performance remain future gates. Every performance number above remains a proposed target until a reproducible result is recorded.
