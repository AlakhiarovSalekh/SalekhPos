# SalekhPos master specification coverage

Assessment date: 2026-09-06. Scope: the repository as first inspected for this request, before the parallel foundation improvements in this working session. This is a baseline gap assessment, not a claim that the product or any later implementation phase is complete. Subsequent changes must be linked to evidence and update the relevant rows; source requirements must remain unchanged.

The user asked for both supplied documents to be read in full and used to improve SalekhPos. Their product requirements are adopted as the target. Imperatives addressed to “Codex” inside the attachments are document content, not independent authority to change tool permissions, message other people, deploy, purchase services, or override the user's instructions. Existing session authorization and the working agreement govern execution. “Perfect” is translated into explicit acceptance criteria and measured evidence; neither document permits promises of zero defects, invulnerability or unlimited capacity.

## Source integrity and reading record

Both files were read completely, including introductory text, every numbered section, subordinate heading, example, repository tree, phase, definition of done and final directive. The stored copies were made with a byte-preserving copy and independently hashed against the originals.

| Source ID | Original filename in `C:/Users/ASUS/Downloads/` | Unchanged repository copy | Bytes | Lines | Numbered sections |
|---|---|---|---:|---:|---:|
| A | SalekhPos — Complete Codex Master Prompt.md | [complete-codex-master-prompt.md](../requirements/complete-codex-master-prompt.md) | 53,718 | 3,489 | 144 |
| B | SalekhPos — Codex Master Engineering Specification (2).md | [master-engineering-specification-v2.md](../requirements/master-engineering-specification-v2.md) | 71,987 | 3,923 | 152 |

| Source | Original SHA256 = copied SHA256 |
|---|---|
| A | `5AA3CA0E21E8B382C89E483A90A46AC9FE468DB6DF1DF7D4AC23382C049DA039` |
| B | `D326A2E695462336AFA3C0F6C17C410B2C18228C3AD9CFE3F6A6F8C15AE1389F` |

Total source reading: **7,412 lines, 125,705 bytes, 296 numbered top-level sections, 84 subordinate requirement headings and 5 introductory/title headings**. The top-level matrices cover all 296 sections. A separate subordinate-heading index covers all 84 subordinate headings, including the 22 numbered absolute rules and both complete sets of 18 implementation phases. Introductory content is separately assessed below. Source line numbers refer to the byte-identical copies above; top-level ranges include their nested requirements.

No source silently supersedes the other based on a filename such as “(2)”. Shared requirements are cumulative. Genuine differences and proposed reconciliations are recorded explicitly below.

## Evidence and status rules

- **Supported**: the stated, explicitly scoped requirement has direct implementation or process evidence. It is not a certification of future code or the whole platform.
- **Partial**: some code or concrete design exists, but important required behavior, configuration or verification is missing. A design alone does not establish implemented product support.
- **Missing**: there is no executable implementation for the requirement in the inspected baseline. Mentioning it in a roadmap does not change this status.

These statuses measure requirements coverage, not security assurance, percent of development effort, or an overall completion percentage. Inherited subordinate status is only an index to the parent row; the parent's stated limitations apply to every child. Tests listed below were inspected. Historical test results are attributed to the existing progress log; this documentation-only audit does not claim to have rerun them.

| Evidence ID | Concrete repository evidence at baseline | What it establishes and what it does not |
|---|---|---|
| E01 | [Money.cs](../../server/src/SharedKernel/Money.cs), [MoneyTests.cs](../../server/tests/SalekhPos.Tests/MoneyTests.cs) | Immutable decimal money; explicit three-uppercase-letter currency syntax; same-currency addition/subtraction; overflow and silent decimal precision-loss detection. No supported-currency registry, multiplication/allocation, settlement rounding, FX persistence, price or tax engine. |
| E02 | [Organization.cs](../../server/src/Modules/Organizations/Organization.cs), [Branch.cs](../../server/src/Modules/Organizations/Branch.cs), [OrganizationRules.cs](../../server/src/Modules/Organizations/OrganizationRules.cs), [OrganizationTests.cs](../../server/tests/SalekhPos.Tests/OrganizationTests.cs) | Nonempty UUIDs, scoped immutable branch identity, validated codes, Unicode names and deactivation. No Business/Brand, Region, warehouse, terminal, membership, explicit branch timezone or public business API. |
| E03 | [001_organizations.sql](../../infra/postgres/migrations/001_organizations.sql), [001_organization_isolation.sql](../../infra/postgres/tests/001_organization_isolation.sql) | Two PostgreSQL tables, scoped primary/unique keys, FK, creation timestamps, forced RLS and restricted runtime grants; SQL cases for tenant context and no DELETE/DDL rights. No authenticated application context, pool integration, business/branch authorization, audit, version tokens or migration runner. |
| E04 | [Program.cs](../../server/src/Api/Program.cs), [appsettings.json](../../server/src/Api/appsettings.json), [HealthTests.cs](../../server/tests/SalekhPos.Tests/HealthTests.cs) | Only process liveness and deliberately unavailable readiness endpoints; Problem Details setup, no server header and localhost host restrictions. No product endpoints, persistence connection, auth/session system, dependency probes, API versioning or OpenAPI. No fake sale endpoint. |
| E05 | [Directory.Build.props](../../Directory.Build.props), [global.json](../../global.json), [NuGet.Config](../../NuGet.Config), [SalekhPos.slnx](../../SalekhPos.slnx), project files and `packages.lock.json` files | .NET 10 target, SDK 10.0.400 pin, warnings as errors, nullable analysis, deterministic builds and package locks. No checked-in CI pipeline or automated security/architecture/migration release gates at baseline. |
| E06 | [scripts/dotnet.ps1](../../scripts/dotnet.ps1), [scripts/test-postgres.ps1](../../scripts/test-postgres.ps1), [progress.md](../development/progress.md) | Local .NET and disposable PostgreSQL verification scripts. Progress records 39 .NET tests and successful PostgreSQL 18.6 isolation checks before this task. Not evidence of production HA, backup restoration, actual pool isolation, load testing or cross-platform operation. |
| E07 | [technology-and-architecture.md](technology-and-architecture.md) | Proposed architecture, domain boundaries, offline/payment risks, identity direction and operating constraints. Explicitly a design; its historical “no code” statements must be read in their date context. |
| E08 | [acceptance-and-delivery-plan.md](acceptance-and-delivery-plan.md) | ADR register, SEC/PAY/INV/SYN/REC acceptance scenarios, platform spikes and proposed performance/DR targets. Targets are not benchmark results; test scenarios are not executed feature implementations. |
| E09 | [organization-foundation.md](organization-foundation.md) | Explains database tenant boundary, runtime/migration role split, required trusted membership and transaction-local context; explicitly states RLS is not protection against SQL injection or stolen runtime credentials. |
| E10 | [working-agreement.md](../development/working-agreement.md), [README.md](../../README.md), this report and source copies | User's persistent work direction, honest readiness boundary, recorded source requirements and reviewable gap assessment. No authorization to call unfinished functions production ready. |
| E11 | Full initial file inventory: `server/src/{Api,SharedKernel,Modules/Organizations}`, `server/tests/SalekhPos.Tests`, `infra/postgres/{migrations,tests}`, `scripts`, `docs`, root configuration files | No `apps` client, worker process, catalog, inventory, sales, payment, sync, subscription or other product implementation. Absence is based on the full repository inventory and reading of all application source, test source, project files, SQL and scripts; not a keyword-only search. |
| E12 | Initial Git status and root dotfiles | All initial project files were untracked on the local foundation branch; `.editorconfig` and `.gitignore` existed, but no checked-in CI/Docker/IaC/release signing artifacts or established commit history. Source-control hygiene still needed. |

## Current state assessment

The repository is an early .NET modular-monolith foundation. It has a useful exact-money primitive, Organization/Branch domain records, the first PostgreSQL schema and isolation checks, and a deliberately restricted health-only API. Those pieces are reusable. There is no operational POS application or store workflow yet. Missing features are not broken existing features; they are work still to implement. The first task is to extend the foundation without falsely reporting readiness.

The existing README is conservative but imprecise about the database: PostgreSQL schema/test artifacts exist, while the API has no persistence integration. The earlier architecture/acceptance documents contain historical “not implemented yet” statements; the latest progress log and code must determine execution status. The coverage tables intentionally capture the initial snapshot so parallel improvements cannot be counted without reviewing their resulting evidence.

## Target architecture derived from both documents

Keep the working .NET modular-monolith layout and PostgreSQL primary store. Organize real features around explicit module contracts, domain/application validation and controlled persistence; introduce workers only for actual durable jobs. The hierarchy must support Platform → Organization/Tenant → Business/Brand → optional Region → Branch and stock locations, with users/devices assigned through scoped relationships rather than forced into a single tree.

Use server-derived authenticated tenant context, scoped permissions, protected Platform Owner operations, resource-safe queries and RLS as defense in depth. Money, quantities, historical prices/tax/FX, inventory ledger, cash ledger, sale/payment/refund states, idempotency and an atomic outbox are core data contracts. Durable local SQLite and an explicit acknowledged/resumable sync protocol are required before offline POS is considered supported. External payment/fiscal calls use adapters and persisted intermediate states, not open database transactions across network calls.

Native desktop/mobile clients may use Flutter after platform, hardware and local durability spikes. The web decision must explicitly reconcile the prior Flutter-web proposal with source A's React + TypeScript direction; there is no existing working client to preserve. React + TypeScript for management and Flutter for native POS is the strict common reading of both new documents unless an explicit ADR justifies another choice. Client UI, country rules, supported fiscal/payment providers and deployment tier remain implementation and validation work.

Separate durable truth from caches and optional systems. Add tenant-safe jobs/object storage/realtime when needed. Production release requires secure configuration, observed dependency health, logs/metrics/traces, backup restoration, incident and recovery procedures, compatible signed client updates and measured performance. Infrastructure capacity and SLA/RPO/RTO claims require evidence on the chosen deployment.

## Requirement differences and decisions to preserve

| ID | Sources / existing evidence | Difference or ambiguity | Reconciliation and implementation consequence |
|---|---|---|---|
| D01 | A68–71; B52–53, B103; E07 | A supplies a detailed target tree and React web. B permits justified repository/framework reality and explicitly rejects arbitrary nesting. Existing proposal prefers Flutter web; no client exists. | Keep usable `server/` and `infra/` rather than mass rename/create empty folders. Record the web framework decision in an ADR before client implementation; React web + Flutter native satisfies both new directions without claiming prior Flutter proposal already proved. |
| D02 | A13; B4; E02/E03/E07 | Both attachments require multi-business hierarchy. Existing code links branches directly to Organization and the earlier proposal assumes a simplified hierarchy. | Add explicit Business/Brand ownership and optional Region with tenant-safe composite references before branch CRUD is public. Preserve Organization as tenant identity. Model user/warehouse relationships flexibly. |
| D03 | A21–24; B10–12, B122; E01/E02 | Branch timezone is mandatory, and B makes a business base currency explicit. Existing Branch has neither; Money checks syntax only. | Add validated timezone/locale/currency context with versioned configuration. Do not infer currency from language or hardcode a legal/tax model. |
| D04 | A67; B52; E07 | A says use a modular monolith; B allows another structure if evidence justifies it. | Existing foundation matches both; retain it. Extract services only after measured needs. |
| D05 | A69 versus A70, A133; B103, B137 | Huge example tree coexists with explicit instructions against useless folders and needless complexity. | Treat the tree as responsibility coverage and future placement guidance, not an order to generate hundreds of placeholders. Every required responsibility remains in this matrix. |
| D06 | A6–12, A136 phase 14; B8, B140 phase 14 | Subscription is to be designed from the start but completed in a later phase. | Define feature entitlement/resource-limit contracts early, keep them distinct from authorization, then implement billing/state/downgrade functions at their dependency-safe phase. A plan-name check never authorizes a request. |
| D07 | A38/A68/A136; B24–26/B140; E07 | Offline-first architecture is mandatory while detailed sync implementation follows basic POS/payment stages; browser durability differs. | Design IDs, snapshots, idempotency and transactions now. Early online POS remains explicitly online-only until real local durability/sync is implemented and tested. Browser offline requires separate proof. |
| D08 | A14–15/A66; B5/B50–51; E07 | Highest platform authority could be misread as unrestricted silent customer-data access. | Owner authority governs platform administration. Support/tenant-data access remains separately scoped and audited; strong authentication, recovery and owner grant restrictions are mandatory. |
| D09 | A77/A112; B57/B102 | Recoverable deletion and privacy deletion/legal holds coexist. | Define entity-specific lifecycle and retention, separate personal identifiers from immutable business records, and do not cascade-delete financial history or downgrade data. |
| D10 | A84; B64/B120; E07 | Negative inventory is allowed only by explicit policy; disconnected stores cannot guarantee global real-time stock. | Online transactional controls plus explicit offline allowance/reconciliation. Do not silently invent negative-stock policy or claim impossible cross-device guarantees during disconnection. |
| D11 | A94/A106; B1.2/B80–82/B95; E08 | Requirements demand speed and resilience; earlier documents list numeric proposals. | Record those numbers as candidate test budgets only. Publish capacity, SLA or zero-RPO claims only after workload/HA/recovery evidence. |
| D12 | A24/A110/A111/A140; B12/B100–102/B143/B146 | Both demand extensible country, fiscal, payment and finance features; no jurisdiction/provider credentials or device models are specified. | Build provider-independent contracts and controlled unsupported states; production adapter/legal acceptance is a separate concrete gate. Never invent tax/legal rules or present mocks as production integration. |
| D13 | A136 security from phase 1; B140 same; E08 old phase-review language | An older suggested design-review checkpoint can be misread as requiring generic permission again. | User's present instruction and working agreement authorize ongoing reversible implementation. Continue the safe next phase; only genuinely missing high-impact decisions or external approval constraints block dependent work. |
| D14 | A28–29/A45/A61; B16/B30/B46/B58/B120–122/B133 | B adds detail on history, tax components, replay scope, refund allocation and authoritative invariants; these are expansions, not replacements. | Retain all A responsibilities and implement B's stricter data semantics alongside them. Do not lose A's explicit fiscal-device, reconciliation, retry, or audit details when consolidating modules. |

## Risk register and acceptance implications

| Risk | Severity / evidence | Required closure evidence |
|---|---|---|
| R01 — No authentication, server-derived tenant membership or resource authorization | Release blocker; E04/E09. RLS alone does not authorize users or branch scope. | Deny-by-default policies; validated issuer/audience/signature/lifetime; membership and scope tests; no client-chosen identity; privileged owner boundaries. |
| R02 — Incomplete business hierarchy and missing branch timezone | High; E02/E03. Extending the wrong ownership key later creates costly migration and isolation risks. | Business/region/branch contracts, composite FK checks, time/locale/currency validation, cross-business/tenant tests and explicit migration plan. |
| R03 — RLS depends on mutable transaction context; no application/pool verification | High; E03/E09. Runtime can set its GUC; SQL injection or leaked credentials are outside the RLS trust guarantee. | Parameterized access, nonprivileged pool credentials, authenticated context, pool reset/commit/rollback/cancel tests; branch scope independently checked. |
| R04 — SQL name constraints differ from domain validation | Medium; E02/E03. Baseline SQL trims ASCII spaces and measures characters but does not match all .NET Unicode whitespace/control rejection. | Document common rule and tests through direct SQL plus application persistence; reject control/invalid names at authoritative boundaries. |
| R05 — No atomic sale/payment/inventory/idempotency/audit/outbox | Release blocker; E11. There is no complete durable store transaction. | Transaction, duplicate/payload conflict, concurrent stock/refund, crash/rollback and replay reconciliation tests before sales exposure. |
| R06 — No currency registry, rounding policy, FX or tax engine | Release blocker for real financial flows; E01. Three-letter syntax does not establish a supported currency or settlement behavior. | Versioned currency and country rules, deterministic arithmetic/allocation fixtures, persisted historical snapshots and provider/receipt total reconciliation. |
| R07 — No durable local client or sync protocol | Release blocker for offline claims; E11. | Real SQLite transactions, copied-device protection, crash-safe migrations, durable queues, acknowledgments/checkpoints and reconnect/duplicate/version-conflict tests. |
| R08 — No production backup, restore, failover or HA proof | Release blocker; E06/E11. A disposable SQL test cluster is not a backup or deployment architecture. | Encrypted independently protected backups, verified restore drills, failover tests, measured RPO/RTO and staffed incident ownership. |
| R09 — No client/hardware/provider evidence | Release blocker for six-platform, receipt, fiscal and card claims; E11/E08. | Real platform builds, accessibility/performance checks, signed artifacts, exact device/firmware matrix and provider sandbox/production acceptance. |
| R10 — No CI/release gates or measured capacity at baseline | High; E05/E08/E12. | Reproducible pipeline with build, relevant tests, static/security/secret/migration checks; workload benchmarks and release rollback verification. |
| R11 — Stale prose and untracked source can obscure real status | Medium; E07/E08/E12. | Update progress/status after each verified unit, record logical commits when appropriate, keep baseline and changed evidence distinct. |
| R12 — Money exactness guard uses arbitrary-precision checking on each add/subtract | Performance uncertainty, not a demonstrated bug; E01. | Benchmark realistic pricing/cart workloads before changing an integrity safeguard; prove replacement retains exactness if optimization becomes necessary. |

## Dependency-ordered implementation roadmap

The full phase order in A136 and B140 is retained. A phase closes only with its applicable definition of done; Phase 17 is additional hardening, never deferred security. Independent platform/hardware feasibility spikes should run early so irreversible choices are not postponed until after all backend work.

| Phase | Concrete delivery and gate |
|---|---|
| 0 — Stabilization | Preserve source requirements, baseline build/tests, locked dependencies, format/static checks, secret hygiene and CI; maintain clean reviewable source state. |
| 1 — Foundation | Module/contract boundaries, safe errors, validation, structured observability, persistence/migration strategy and architecture checks. |
| 2 — Identity/tenancy | Provider-validated identity, sessions/MFA capability, Organization→Business→Branch/Region, memberships/scopes, owner protections, real pool/RLS isolation tests. No unauthenticated business CRUD. |
| 3 — Global configuration | Supported currency/timezone/locale, versioned rounding/tax/country adapters, configuration override rules and cross-language arithmetic/time fixtures. |
| 4 — Catalog | Product/variant/barcode/unit models, tenant-safe bounded search, import contracts and historical-reference policy. |
| 5 — Inventory | Locations, traceable movements and projections, states, lot/serial/count/adjustment semantics, authoritative consistency and concurrent updates. |
| 6 — Pricing | Price books, scheduled prices, deterministic promotion stacking, override thresholds and historical tax/price snapshots. |
| 7 — POS | Register/shift/cart/cash, transactional sale+stock+cash+audit+outbox, duplicate protection, receipt jobs and failure states. |
| 8 — Payments | Provider abstractions, split allocation/refund rules, persisted uncertain states, idempotency, webhook authentication and reconciliation. |
| 9 — Offline/sync | Real local store/outbox, versioned resumable protocol, device trust, checkpoints, local recovery and incompatible-client handling. |
| 10 — Procurement | Supplier/PO/receiving/returns, partial delivery, approvals and consistent inventory effects. |
| 11 — CRM/loyalty | Minimized personal data, consent, audited points/credit/gift value and duplicate-redemption protection. |
| 12 — Employees/approvals | Scoped assignments, generic thresholds/multiple approvers/expiry/rejection and personal-data access limits. |
| 13 — Reporting | Defined metrics, tenant-safe asynchronous exports and summaries that do not starve checkout. |
| 14 — Subscriptions | Entitlements, resource limits/add-ons, regional billing prices, lifecycle/grace, upgrade/downgrade data preservation. |
| 15 — Platform admin | Strongly protected owner/admin console, limited audited support sessions and platform monitoring. |
| 16 — Integrations | Actual fiscal/payment/accounting/ecommerce adapters, vendor contracts, configuration and sandbox-to-production acceptance. |
| 17 — Hardening | Scenario-specific security, mixed-workload load/stress, chaos, backup restoration, disaster recovery and staged signed releases. |

## Introductory requirements

| ID | Source lines / covered headings | Status | Evidence / remaining gap |
|---|---|---|---|
| A-INTRO | A1–78; three opening headings and platform-wide introductory requirements | Partial | E01–E12 provide a reusable foundation and design only. Global retail SaaS, six clients, offline, multi-business, durability, HA and all product domains are target scope, not delivered capability. |
| B-INTRO | B1–69; opening title/subtitle and complete introductory engineering checklist | Partial | Same baseline. E10 preserves honest claims; actual authentication, authorization, synchronization, failure recovery, accessibility and production evidence remain required. |

## Source A: all 144 numbered sections

Each row includes all bullets, examples and instructions within its source range; concise gap text is an index, not a replacement for the unchanged requirement text.

| Requirement ID | Exact source heading | Source lines | Baseline status | Evidence and required closure |
|---|---|---|---|---|
| A1 | FUNDAMENTAL ENGINEERING PHILOSOPHY | 79–123 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A2 | ABSOLUTE RULES | 124–285 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A3 | PRODUCT DEFINITION | 286–328 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A4 | SUPPORTED PLATFORMS | 329–385 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A5 | BUSINESS SIZE MODEL | 386–438 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A6 | SUBSCRIPTION MODEL | 439–476 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A7 | SUBSCRIPTION ENTITLEMENTS | 477–503 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A8 | RESOURCE LIMITS | 504–517 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A9 | ADD-ONS | 518–534 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A10 | SUBSCRIPTION STATE MACHINE | 535–552 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A11 | DOWNGRADE SAFETY | 553–573 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A12 | REGIONAL SUBSCRIPTION PRICING | 574–591 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A13 | BUSINESS HIERARCHY | 592–613 | Partial | E02/E03: initial organization/branch domain, composite keys and SQL RLS only. Business hierarchy, authenticated scope, pooled access and remaining domain constraints require implementation and tests. |
| A14 | PLATFORM OWNER | 614–637 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A15 | PLATFORM OWNER SECURITY | 638–654 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A16 | DEFAULT BUSINESS ROLES | 655–685 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A17 | PERMISSION ENGINE | 686–731 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A18 | PERMISSION SCOPES | 732–757 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A19 | GLOBALIZATION | 758–780 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A20 | RTL SUPPORT | 781–791 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A21 | TIMEZONES | 792–803 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A22 | CURRENCIES | 804–816 | Partial | E01: decimal addition/subtraction and currency syntax only. Currency registry, rounding, FX/tax snapshots and financial workflows remain. |
| A23 | COUNTRY CONFIGURATION | 817–836 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A24 | COUNTRY ADAPTERS | 837–856 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A25 | PRODUCT CATALOG | 857–883 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A26 | PRODUCT VARIANTS | 884–903 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A27 | BARCODE ENGINE | 904–917 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A28 | INVENTORY LEDGER | 918–952 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A29 | INVENTORY STATES | 953–967 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A30 | WAREHOUSE LOCATIONS | 968–982 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A31 | BATCH / LOT / EXPIRY | 983–996 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A32 | SERIAL TRACKING | 997–1006 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A33 | INVENTORY COUNT | 1007–1020 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A34 | PROCUREMENT | 1021–1045 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A35 | SUPPLIERS | 1046–1063 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A36 | AUTOMATIC REORDER | 1064–1079 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A37 | POS CORE | 1080–1098 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A38 | OFFLINE POS | 1099–1114 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A39 | LOCAL DATABASE | 1115–1132 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A40 | SYNC ENGINE | 1133–1163 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A41 | INITIAL SYNC | 1164–1176 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A42 | CONFLICT RESOLUTION | 1177–1193 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A43 | OUTBOX / INBOX | 1194–1205 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A44 | CART RECOVERY | 1206–1215 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A45 | SALES MODEL | 1216–1244 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A46 | PRICING ENGINE | 1245–1260 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A47 | PROMOTIONS | 1261–1279 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A48 | MANUAL DISCOUNTS | 1280–1291 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A49 | PAYMENTS | 1292–1307 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A50 | PAYMENT IDEMPOTENCY | 1308–1322 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A51 | SPLIT PAYMENT | 1323–1336 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A52 | CASH MANAGEMENT | 1337–1353 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A53 | SHIFTS | 1354–1366 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A54 | RETURNS | 1367–1388 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A55 | RECEIPTS | 1389–1401 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A56 | HARDWARE ABSTRACTION | 1402–1418 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A57 | CUSTOMERS / CRM | 1419–1436 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A58 | LOYALTY | 1437–1452 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A59 | EMPLOYEES | 1453–1469 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A60 | APPROVAL ENGINE | 1470–1487 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A61 | AUDIT | 1488–1512 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A62 | LOSS PREVENTION | 1513–1529 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A63 | REPORTING | 1530–1544 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A64 | OWNER DASHBOARD | 1545–1564 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A65 | SUPER ADMIN DASHBOARD | 1565–1589 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A66 | SUPPORT MODE | 1590–1604 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A67 | BACKEND ARCHITECTURE | 1605–1614 | Partial | E02/E05/E07: small .NET foundation and target design. Full module boundaries, clients, contracts, workers and operational topology remain. |
| A68 | BACKEND TECHNOLOGY | 1615–1655 | Partial | E02/E05/E07: small .NET foundation and target design. Full module boundaries, clients, contracts, workers and operational topology remain. |
| A69 | COMPLETE REPOSITORY STRUCTURE | 1656–2026 | Partial | E02/E05/E07: small .NET foundation and target design. Full module boundaries, clients, contracts, workers and operational topology remain. |
| A70 | MODULE INTERNAL STRUCTURE | 2027–2080 | Partial | E02/E05/E07: small .NET foundation and target design. Full module boundaries, clients, contracts, workers and operational topology remain. |
| A71 | FRONTEND FEATURE STRUCTURE | 2081–2110 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A72 | DATABASE | 2111–2133 | Partial | E02/E03: initial organization/branch domain, composite keys and SQL RLS only. Business hierarchy, authenticated scope, pooled access and remaining domain constraints require implementation and tests. |
| A73 | DATABASE HIGH AVAILABILITY | 2134–2151 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A74 | DATA LOSS PROTECTION | 2152–2168 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A75 | BACKUP SECURITY | 2169–2176 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A76 | RESTORE TESTING | 2177–2188 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A77 | SOFT DELETE | 2189–2196 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A78 | CACHE | 2197–2212 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A79 | QUEUES | 2213–2228 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A80 | JOB FAILURE | 2229–2240 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A81 | HORIZONTAL SCALING | 2241–2254 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A82 | GRACEFUL DEGRADATION | 2255–2275 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A83 | API | 2276–2292 | Partial | E04: minimal health HTTP host, logging configuration and ProblemDetails setup. Product API contracts, protected errors, telemetry and operational evidence remain. |
| A84 | CONCURRENCY | 2293–2312 | Partial | E01: decimal addition/subtraction and currency syntax only. Currency registry, rounding, FX/tax snapshots and financial workflows remain. |
| A85 | AUTHENTICATION | 2313–2328 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A86 | SECURITY PROTECTIONS | 2329–2350 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A87 | SECRETS | 2351–2363 | Partial | E05/E06: pinned SDK/packages, strict compilation and local scripts only. CI, scans, signing, secret management and release enforcement remain. |
| A88 | OBJECT STORAGE | 2364–2376 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A89 | GLOBAL INFRASTRUCTURE | 2377–2389 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A90 | DATA RESIDENCY | 2390–2401 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A91 | DEVICE MANAGEMENT | 2402–2418 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A92 | TERMINAL PROVISIONING | 2419–2434 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A93 | PERFORMANCE | 2435–2447 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A94 | PERFORMANCE SLOS | 2448–2463 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A95 | OBSERVABILITY | 2464–2476 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A96 | MONITORING METRICS | 2477–2498 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A97 | HEALTH CHECKS | 2499–2517 | Partial | E04: liveness and explicitly unavailable readiness only. Dependency probes and production health policy remain. |
| A98 | DEPLOYMENT | 2518–2533 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A99 | DATABASE MIGRATIONS | 2534–2545 | Partial | E01/E03/E06: narrow money/domain and SQL isolation checks. Feature-specific authorization, concurrency, crash, pool, recovery and workload tests remain. |
| A100 | FEATURE FLAGS | 2546–2559 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A101 | ENVIRONMENTS | 2560–2572 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A102 | TESTING | 2573–2596 | Partial | E01/E03/E06: narrow money/domain and SQL isolation checks. Feature-specific authorization, concurrency, crash, pool, recovery and workload tests remain. |
| A103 | SECURITY TESTS | 2597–2615 | Partial | E01/E03/E06: narrow money/domain and SQL isolation checks. Feature-specific authorization, concurrency, crash, pool, recovery and workload tests remain. |
| A104 | LOAD TESTING | 2616–2633 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A105 | CHAOS TESTING | 2634–2651 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A106 | DISASTER RECOVERY | 2652–2667 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A107 | IMPORT | 2668–2682 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A108 | EXPORT | 2683–2690 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A109 | NOTIFICATIONS | 2691–2704 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A110 | INTEGRATIONS | 2705–2722 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A111 | FINANCE | 2723–2740 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A112 | PRIVACY | 2741–2753 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A113 | ACCESSIBILITY | 2754–2768 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A114 | CONFIGURATION HIERARCHY | 2769–2781 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A115 | FEATURE AVAILABILITY FORMULA | 2782–2799 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A116 | ENTITY IDS | 2800–2809 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A117 | DOMAIN EVENTS | 2810–2825 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A118 | STATE MACHINES | 2826–2842 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A119 | SALE / PAYMENT ORCHESTRATION | 2843–2854 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A120 | REPORTING ISOLATION | 2855–2867 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A121 | MOBILE VERSION COMPATIBILITY | 2868–2875 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A122 | DESKTOP UPDATES | 2876–2889 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A123 | SUPPLY CHAIN SECURITY | 2890–2901 | Partial | E05/E06: pinned SDK/packages, strict compilation and local scripts only. CI, scans, signing, secret management and release enforcement remain. |
| A124 | INCIDENT RESPONSE | 2902–2922 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A125 | DOCUMENTATION | 2923–2947 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A126 | ADRs | 2948–2962 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A127 | CODE QUALITY | 2963–2978 | Partial | E05/E06: pinned SDK/packages, strict compilation and local scripts only. CI, scans, signing, secret management and release enforcement remain. |
| A128 | DEPENDENCIES | 2979–2991 | Partial | E05/E06: pinned SDK/packages, strict compilation and local scripts only. CI, scans, signing, secret management and release enforcement remain. |
| A129 | PRODUCTION ACCESS | 2992–3005 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A130 | LOGGING | 3006–3018 | Partial | E04: minimal health HTTP host, logging configuration and ProblemDetails setup. Product API contracts, protected errors, telemetry and operational evidence remain. |
| A131 | ERROR HANDLING | 3019–3030 | Partial | E04: minimal health HTTP host, logging configuration and ProblemDetails setup. Product API contracts, protected errors, telemetry and operational evidence remain. |
| A132 | USER FAILURE MESSAGES | 3031–3046 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| A133 | DO NOT OVERENGINEER | 3047–3063 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A134 | IMPLEMENTATION WORKFLOW FOR CODEX | 3064–3087 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A135 | FIRST CODEX DELIVERABLE | 3088–3129 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A136 | IMPLEMENTATION PHASES | 3130–3283 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A137 | DEFINITION OF DONE | 3284–3311 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A138 | STOP CONDITIONS | 3312–3334 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A139 | NO FAKE PRODUCTION LOGIC | 3335–3348 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A140 | NO FAKE INTEGRATIONS | 3349–3361 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A141 | NO FAKE SUCCESS | 3362–3372 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A142 | VERSION CONTROL DISCIPLINE | 3373–3388 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A143 | FINAL NON-NEGOTIABLE PRINCIPLES | 3389–3426 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| A144 | FINAL DIRECTIVE TO CODEX | 3427–3489 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |

## Source B: all 152 numbered parts

The related A IDs identify common requirements. B-only expansions are explicitly retained in their row; a cross-reference does not override either source.

| Requirement ID | Exact source heading | Source lines | Baseline status | Evidence and required closure |
|---|---|---|---|---|
| B1 | NON-NEGOTIABLE ENGINEERING RULES | 70–258 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B2 | PRODUCT DEFINITION | 259–287 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B3 | BUSINESS SCALE MODEL | 288–343 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B4 | HIGH-LEVEL DOMAIN HIERARCHY | 344–374 | Partial | E02/E03: initial organization/branch domain, composite keys and SQL RLS only. Business hierarchy, authenticated scope, pooled access and remaining domain constraints require implementation and tests. |
| B5 | PLATFORM OWNERSHIP | 375–421 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B6 | TENANT ISOLATION | 422–453 | Partial | E02/E03: initial organization/branch domain, composite keys and SQL RLS only. Business hierarchy, authenticated scope, pooled access and remaining domain constraints require implementation and tests. |
| B7 | ROLE AND PERMISSION SYSTEM | 454–559 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B8 | SUBSCRIPTION ARCHITECTURE | 560–698 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B9 | INTERNATIONALIZATION AND LOCALIZATION | 699–726 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B10 | TIME | 727–748 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B11 | CURRENCIES AND FINANCIAL CALCULATION | 749–776 | Partial | E01: decimal addition/subtraction and currency syntax only. Currency registry, rounding, FX/tax snapshots and financial workflows remain. |
| B12 | COUNTRY CONFIGURATION | 777–804 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B13 | PRODUCT CATALOG | 805–841 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B14 | PRODUCT VARIANTS | 842–866 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B15 | BARCODE SYSTEM | 867–888 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B16 | INVENTORY MODEL | 889–924 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B17 | STOCK LOCATIONS | 925–941 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B18 | BATCH, LOT, EXPIRY AND SERIAL TRACKING | 942–962 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B19 | STOCK COUNTING | 963–985 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B20 | PROCUREMENT | 986–1012 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B21 | SUPPLIERS | 1013–1031 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B22 | REORDERING | 1032–1051 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B23 | POS CORE | 1052–1074 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B24 | OFFLINE POS | 1075–1097 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B25 | LOCAL DURABLE DATABASE | 1098–1120 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B26 | SYNC ENGINE | 1121–1160 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B27 | CONFLICT RESOLUTION | 1161–1183 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B28 | TRANSACTIONAL OUTBOX / INBOX | 1184–1199 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B29 | CART RECOVERY | 1200–1211 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B30 | SALES | 1212–1243 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B31 | PRICING | 1244–1261 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B32 | PROMOTIONS | 1262–1282 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B33 | MANUAL DISCOUNTS AND PRICE OVERRIDES | 1283–1294 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B34 | PAYMENT MODEL | 1295–1312 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B35 | PAYMENT IDEMPOTENCY | 1313–1328 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B36 | SPLIT PAYMENTS | 1329–1344 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B37 | CASH MANAGEMENT | 1345–1363 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B38 | SHIFTS | 1364–1382 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B39 | RETURNS AND REFUNDS | 1383–1407 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B40 | RECEIPTS | 1408–1422 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B41 | HARDWARE ABSTRACTION | 1423–1441 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B42 | CUSTOMERS / CRM | 1442–1464 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B43 | LOYALTY | 1465–1482 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B44 | EMPLOYEES | 1483–1500 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B45 | APPROVAL ENGINE | 1501–1530 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B46 | AUDIT | 1531–1559 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B47 | LOSS PREVENTION | 1560–1578 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B48 | REPORTING | 1579–1593 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B49 | DASHBOARDS | 1594–1617 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B50 | PLATFORM ADMINISTRATION | 1618–1648 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B51 | SUPPORT ACCESS | 1649–1665 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B52 | BACKEND ARCHITECTURE | 1666–1712 | Partial | E02/E05/E07: small .NET foundation and target design. Full module boundaries, clients, contracts, workers and operational topology remain. |
| B53 | TECHNOLOGY DIRECTION | 1713–1760 | Partial | E02/E05/E07: small .NET foundation and target design. Full module boundaries, clients, contracts, workers and operational topology remain. |
| B54 | DATABASE | 1761–1788 | Partial | E02/E03: initial organization/branch domain, composite keys and SQL RLS only. Business hierarchy, authenticated scope, pooled access and remaining domain constraints require implementation and tests. |
| B55 | DATABASE HIGH AVAILABILITY | 1789–1803 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B56 | DATABASE DATA PROTECTION | 1804–1828 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B57 | DELETION | 1829–1845 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B58 | VERSIONING AND HISTORY | 1846–1862 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B59 | CACHE | 1863–1880 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B60 | MESSAGE QUEUE AND WORKERS | 1881–1910 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B61 | GRACEFUL DEGRADATION | 1911–1933 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B62 | API ARCHITECTURE | 1934–1949 | Partial | E02/E05/E07: small .NET foundation and target design. Full module boundaries, clients, contracts, workers and operational topology remain. |
| B63 | IDEMPOTENCY | 1950–1968 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B64 | CONCURRENCY | 1969–1992 | Partial | E01: decimal addition/subtraction and currency syntax only. Currency registry, rounding, FX/tax snapshots and financial workflows remain. |
| B65 | AUTHENTICATION | 1993–2013 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B66 | MFA AND PASSKEYS | 2014–2030 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B67 | AUTHORIZATION | 2031–2051 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B68 | SECURITY CONTROLS | 2052–2079 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B69 | SECRET MANAGEMENT | 2080–2101 | Partial | E05/E06: pinned SDK/packages, strict compilation and local scripts only. CI, scans, signing, secret management and release enforcement remain. |
| B70 | DATA CLASSIFICATION | 2102–2123 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B71 | FILE/OBJECT STORAGE | 2124–2140 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B72 | GLOBAL INFRASTRUCTURE | 2141–2158 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B73 | DATA RESIDENCY | 2159–2175 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B74 | CLIENT APPLICATION STRATEGY | 2176–2196 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B75 | DESKTOP HARDWARE | 2197–2213 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B76 | DEVICE MANAGEMENT | 2214–2235 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B77 | TERMINAL PROVISIONING | 2236–2252 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B78 | INITIAL SYNC | 2253–2269 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B79 | CLIENT PERFORMANCE | 2270–2282 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B80 | PERFORMANCE TARGETS | 2283–2304 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B81 | SCALABILITY | 2305–2329 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B82 | NO SINGLE POINT OF FAILURE | 2330–2347 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B83 | OBSERVABILITY | 2348–2372 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B84 | METRICS | 2373–2395 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B85 | ALERTS | 2396–2419 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B86 | HEALTH CHECKS | 2420–2437 | Partial | E04: liveness and explicitly unavailable readiness only. Dependency probes and production health policy remain. |
| B87 | DEPLOYMENT | 2438–2455 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B88 | DATABASE MIGRATIONS | 2456–2477 | Partial | E01/E03/E06: narrow money/domain and SQL isolation checks. Feature-specific authorization, concurrency, crash, pool, recovery and workload tests remain. |
| B89 | FEATURE FLAGS | 2478–2493 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B90 | RELEASE ENVIRONMENTS | 2494–2506 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B91 | TESTING REQUIREMENTS | 2507–2530 | Partial | E01/E03/E06: narrow money/domain and SQL isolation checks. Feature-specific authorization, concurrency, crash, pool, recovery and workload tests remain. |
| B92 | SECURITY TESTING | 2531–2549 | Partial | E01/E03/E06: narrow money/domain and SQL isolation checks. Feature-specific authorization, concurrency, crash, pool, recovery and workload tests remain. |
| B93 | LOAD TESTING | 2550–2574 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B94 | CHAOS AND FAILURE TESTING | 2575–2595 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B95 | DISASTER RECOVERY | 2596–2617 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B96 | BACKUP VALIDATION | 2618–2637 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B97 | IMPORT | 2638–2654 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B98 | EXPORT | 2655–2664 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B99 | NOTIFICATIONS | 2665–2689 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B100 | INTEGRATIONS | 2690–2715 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B101 | ACCOUNTING / FINANCE | 2716–2738 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B102 | PRIVACY AND RETENTION | 2739–2757 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B103 | REPOSITORY STRUCTURE | 2758–2786 | Partial | E02/E05/E07: small .NET foundation and target design. Full module boundaries, clients, contracts, workers and operational topology remain. |
| B104 | ARCHITECTURE DOCUMENTATION | 2787–2819 | Partial | E02/E05/E07: small .NET foundation and target design. Full module boundaries, clients, contracts, workers and operational topology remain. |
| B105 | API DOCUMENTATION | 2820–2836 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B106 | CODE QUALITY | 2837–2856 | Partial | E05/E06: pinned SDK/packages, strict compilation and local scripts only. CI, scans, signing, secret management and release enforcement remain. |
| B107 | DEPENDENCIES | 2857–2873 | Partial | E05/E06: pinned SDK/packages, strict compilation and local scripts only. CI, scans, signing, secret management and release enforcement remain. |
| B108 | PRODUCTION ACCESS | 2874–2888 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B109 | LOGGING | 2889–2906 | Partial | E04: minimal health HTTP host, logging configuration and ProblemDetails setup. Product API contracts, protected errors, telemetry and operational evidence remain. |
| B110 | ERROR HANDLING | 2907–2920 | Partial | E04: minimal health HTTP host, logging configuration and ProblemDetails setup. Product API contracts, protected errors, telemetry and operational evidence remain. |
| B111 | USER EXPERIENCE DURING FAILURE | 2921–2942 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B112 | ACCESSIBILITY | 2943–2957 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B113 | UI DESIGN PRINCIPLES | 2958–2981 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B114 | CONFIGURATION HIERARCHY | 2982–3000 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B115 | FEATURE AVAILABILITY | 3001–3022 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B116 | ENTITY IDENTIFIERS | 3023–3034 | Partial | E02/E03: initial organization/branch domain, composite keys and SQL RLS only. Business hierarchy, authenticated scope, pooled access and remaining domain constraints require implementation and tests. |
| B117 | DOMAIN EVENTS | 3035–3052 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B118 | STATE MACHINES | 3053–3082 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B119 | PAYMENT/SALE CONSISTENCY | 3083–3096 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B120 | INVENTORY CONSISTENCY | 3097–3121 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B121 | SALES SNAPSHOT | 3122–3137 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B122 | TAX CALCULATION | 3138–3156 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B123 | SEARCH | 3157–3168 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B124 | ARCHIVAL | 3169–3178 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B125 | SCALING DATABASE | 3179–3196 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B126 | REPORTING ISOLATION | 3197–3211 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B127 | FEATURE MODULARITY | 3212–3223 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B128 | MOBILE APP VERSIONING | 3224–3233 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B129 | DESKTOP UPDATE STRATEGY | 3234–3248 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B130 | SIGNING AND SUPPLY CHAIN | 3249–3260 | Partial | E05/E06: pinned SDK/packages, strict compilation and local scripts only. CI, scans, signing, secret management and release enforcement remain. |
| B131 | SECURITY INCIDENT PREPARATION | 3261–3282 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B132 | BUSINESS CONTINUITY | 3283–3302 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B133 | DATA INTEGRITY CONSTRAINTS | 3303–3319 | Partial | E01/E03/E06: narrow money/domain and SQL isolation checks. Feature-specific authorization, concurrency, crash, pool, recovery and workload tests remain. |
| B134 | SUPPORT SMALL BUSINESSES WITHOUT DUPLICATING PLATFORM | 3320–3334 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B135 | PLAN GROWTH | 3335–3354 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B136 | ENTERPRISE EXTENSIONS | 3355–3376 | Missing | E11: no executable implementation at baseline. Implement every requirement in this source range and satisfy its phase acceptance gate; source detail is retained verbatim. |
| B137 | DO NOT OVERENGINEER | 3377–3397 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B138 | CODEX WORKING PROCEDURE | 3398–3425 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B139 | FIRST REQUIRED OUTPUT FROM CODEX | 3426–3460 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B140 | IMPLEMENTATION ORDER | 3461–3616 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B141 | DEFINITION OF DONE FOR EACH FEATURE | 3617–3644 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B142 | STOP CONDITIONS | 3645–3666 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B143 | SAFE ASSUMPTION POLICY | 3667–3688 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B144 | VERSION CONTROL | 3689–3706 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B145 | NO PLACEHOLDER PRODUCTION BEHAVIOR | 3707–3724 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B146 | NO FAKE INTEGRATIONS | 3725–3738 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B147 | NO FAKE SUCCESS | 3739–3750 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B148 | ERROR RECOVERY | 3751–3767 | Partial | E04: minimal health HTTP host, logging configuration and ProblemDetails setup. Product API contracts, protected errors, telemetry and operational evidence remain. |
| B149 | PERFORMANCE DISCIPLINE | 3768–3786 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B150 | FINAL ARCHITECTURAL PRINCIPLES | 3787–3822 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B151 | EXPECTED END STATE | 3823–3871 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |
| B152 | FINAL CODEX DIRECTIVE | 3872–3923 | Partial | E01–E10: only the explicitly scoped foundation/design/process evidence above. All remaining bullets and acceptance criteria in this source range apply; this is not product completion. |

## Subordinate headings: complete index

All 84 subordinate requirement headings are indexed below. Introductory title/subtitle headings are covered by the introductory rows. A subordinate row inherits only its parent's scoped status and limitations; it does not claim the entire child feature is complete. Numbered absolute rules receive their own status/gap assessment.

| Source / parent | Source line | Subordinate heading | Status |
|---|---:|---|---|
| A2 | 126 | 2.1 Never trust clients | Inherits parent scope and limitations |
| A2 | 156 | 2.2 Never silently lose critical data | Inherits parent scope and limitations |
| A2 | 193 | 2.3 Never use float/double for money | Inherits parent scope and limitations |
| A2 | 203 | 2.4 Never hardcode one country | Inherits parent scope and limitations |
| A2 | 222 | 2.5 Never use UI visibility as authorization | Inherits parent scope and limitations |
| A2 | 230 | 2.6 Never perform dangerous unbounded queries | Inherits parent scope and limitations |
| A2 | 251 | 2.7 Never create unnecessary single points of failure | Inherits parent scope and limitations |
| A2 | 257 | 2.8 Never rely on cache for durable business truth | Inherits parent scope and limitations |
| A2 | 263 | 2.9 Never use unlimited retries | Inherits parent scope and limitations |
| A2 | 275 | 2.10 Never perform irreversible destructive operations casually | Inherits parent scope and limitations |
| A4 | 331 | Web | Inherits parent scope and limitations |
| A4 | 346 | Desktop | Inherits parent scope and limitations |
| A4 | 364 | Mobile | Inherits parent scope and limitations |
| A5 | 390 | SMALL BUSINESS | Inherits parent scope and limitations |
| A5 | 401 | MEDIUM BUSINESS | Inherits parent scope and limitations |
| A5 | 412 | LARGE / ENTERPRISE | Inherits parent scope and limitations |
| A6 | 455 | SMALL | Inherits parent scope and limitations |
| A6 | 461 | MEDIUM | Inherits parent scope and limitations |
| A6 | 467 | LARGE / ENTERPRISE | Inherits parent scope and limitations |
| A135 | 3092 | CURRENT STATE ASSESSMENT | Inherits parent scope and limitations |
| A135 | 3105 | TARGET ARCHITECTURE | Inherits parent scope and limitations |
| A135 | 3120 | GAP ANALYSIS | Inherits parent scope and limitations |
| A135 | 3124 | ROADMAP | Inherits parent scope and limitations |
| A136 | 3132 | PHASE 0 — STABILIZATION | Inherits parent scope and limitations |
| A136 | 3141 | PHASE 1 — ARCHITECTURE FOUNDATION | Inherits parent scope and limitations |
| A136 | 3150 | PHASE 2 — IDENTITY/TENANCY | Inherits parent scope and limitations |
| A136 | 3161 | PHASE 3 — GLOBAL CONFIGURATION | Inherits parent scope and limitations |
| A136 | 3170 | PHASE 4 — CATALOG | Inherits parent scope and limitations |
| A136 | 3178 | PHASE 5 — INVENTORY | Inherits parent scope and limitations |
| A136 | 3187 | PHASE 6 — PRICING/PROMOTIONS | Inherits parent scope and limitations |
| A136 | 3193 | PHASE 7 — POS | Inherits parent scope and limitations |
| A136 | 3202 | PHASE 8 — PAYMENTS | Inherits parent scope and limitations |
| A136 | 3209 | PHASE 9 — OFFLINE/SYNC | Inherits parent scope and limitations |
| A136 | 3217 | PHASE 10 — PROCUREMENT | Inherits parent scope and limitations |
| A136 | 3224 | PHASE 11 — CRM/LOYALTY | Inherits parent scope and limitations |
| A136 | 3231 | PHASE 12 — EMPLOYEES/APPROVALS | Inherits parent scope and limitations |
| A136 | 3237 | PHASE 13 — REPORTING | Inherits parent scope and limitations |
| A136 | 3243 | PHASE 14 — SUBSCRIPTIONS | Inherits parent scope and limitations |
| A136 | 3252 | PHASE 15 — PLATFORM ADMIN | Inherits parent scope and limitations |
| A136 | 3259 | PHASE 16 — INTEGRATIONS | Inherits parent scope and limitations |
| A136 | 3267 | PHASE 17 — HARDENING | Inherits parent scope and limitations |
| B1 | 74 | 1.1 Never knowingly sacrifice correctness for implementation speed | Inherits parent scope and limitations |
| B1 | 95 | 1.2 Never claim mathematical impossibilities | Inherits parent scope and limitations |
| B1 | 109 | 1.3 Never trust a client | Inherits parent scope and limitations |
| B1 | 133 | 1.4 Never silently lose critical data | Inherits parent scope and limitations |
| B1 | 162 | 1.5 Never use floating-point values for money | Inherits parent scope and limitations |
| B1 | 176 | 1.6 Never hardcode country assumptions | Inherits parent scope and limitations |
| B1 | 197 | 1.7 Never implement authorization only in the UI | Inherits parent scope and limitations |
| B1 | 205 | 1.8 Never implement unrestricted queries against unbounded datasets | Inherits parent scope and limitations |
| B1 | 224 | 1.9 Never create an unnecessary single point of failure | Inherits parent scope and limitations |
| B1 | 232 | 1.10 Never make Redis or any cache the only source of critical business data | Inherits parent scope and limitations |
| B1 | 238 | 1.11 Never allow unlimited retries | Inherits parent scope and limitations |
| B1 | 251 | 1.12 Never perform dangerous destructive actions without audit and controls | Inherits parent scope and limitations |
| B2 | 267 | Web | Inherits parent scope and limitations |
| B2 | 271 | Desktop | Inherits parent scope and limitations |
| B2 | 277 | Mobile | Inherits parent scope and limitations |
| B3 | 292 | Small business | Inherits parent scope and limitations |
| B3 | 305 | Medium business | Inherits parent scope and limitations |
| B3 | 316 | Large / enterprise retail | Inherits parent scope and limitations |
| B8 | 572 | Small Business | Inherits parent scope and limitations |
| B8 | 580 | Medium Business | Inherits parent scope and limitations |
| B8 | 588 | Large / Enterprise | Inherits parent scope and limitations |
| B139 | 3430 | Current State Assessment | Inherits parent scope and limitations |
| B139 | 3438 | Target Architecture | Inherits parent scope and limitations |
| B139 | 3449 | Gap Analysis | Inherits parent scope and limitations |
| B139 | 3453 | Implementation Roadmap | Inherits parent scope and limitations |
| B140 | 3465 | Phase 0 — Repository stabilization | Inherits parent scope and limitations |
| B140 | 3474 | Phase 1 — Solution architecture | Inherits parent scope and limitations |
| B140 | 3482 | Phase 2 — Identity and tenancy | Inherits parent scope and limitations |
| B140 | 3495 | Phase 3 — Global configuration | Inherits parent scope and limitations |
| B140 | 3504 | Phase 4 — Catalog | Inherits parent scope and limitations |
| B140 | 3512 | Phase 5 — Inventory | Inherits parent scope and limitations |
| B140 | 3521 | Phase 6 — Pricing | Inherits parent scope and limitations |
| B140 | 3527 | Phase 7 — POS core | Inherits parent scope and limitations |
| B140 | 3536 | Phase 8 — Payments | Inherits parent scope and limitations |
| B140 | 3543 | Phase 9 — Offline and sync | Inherits parent scope and limitations |
| B140 | 3551 | Phase 10 — Procurement | Inherits parent scope and limitations |
| B140 | 3558 | Phase 11 — Customers and loyalty | Inherits parent scope and limitations |
| B140 | 3565 | Phase 12 — Employees and approvals | Inherits parent scope and limitations |
| B140 | 3571 | Phase 13 — Reporting | Inherits parent scope and limitations |
| B140 | 3577 | Phase 14 — Subscription | Inherits parent scope and limitations |
| B140 | 3586 | Phase 15 — Super Admin | Inherits parent scope and limitations |
| B140 | 3592 | Phase 16 — Integrations | Inherits parent scope and limitations |
| B140 | 3599 | Phase 17 — Hardening | Inherits parent scope and limitations |

## Validation of this assessment

All 144 A sections and 152 B parts occur exactly once, in source order, with inclusive line ranges. All 84 subordinate headings are indexed. Source hashes were rechecked. Baseline rows deliberately do not claim later work; see the dated evidence addendum and development/progress.md.

No application code or production environment was changed by this documentation audit. The original attachments remain unmodified. Product completion requires implementing and verifying the missing requirements, not marking the tables complete because the documents have been read.

## 2026-09-07 verified implementation addendum

The matrices above remain the pre-change baseline. Current evidence is in
[progress.md](../development/progress.md), [access-foundation.md](access-foundation.md)
and the referenced code/tests. No whole-platform completion percentage is asserted.

| Requirements advanced | Concrete evidence | Still open |
|---|---|---|
| A13/A18/A19/A21/A72/A84; B4/B6/B7/B9/B10/B54/B64/B67/B133 | Business/Region/Branch domain; migration 002 and Unicode/FK/legacy SQL tests; scoped branch API and live PostgreSQL tests | Warehouses/devices/own/platform scopes, global locale/currency, broader workflows |
| A17/A85/A86/A103; B6/B7/B65–69/B92 | JwtBearer validation, membership/grant schema 003, spoofing/BOLA/revocation tests | Actual OIDC login, MFA/Owner/session/device administration, audited provisioning |
| A83/A93/A95/A97/A130/A131; B62/B79/B83/B86/B105/B109/B110 | Bounded keyset reads, safe errors, headers, trace ID, readiness, OpenAPI contract | Measured load budgets, telemetry exporters/alerts and remaining API domains |
| A74/A76/A102/A105; B56/B91/B94/B96/B148 | Three migration suites, separate logical restore database, tests against restored schema/data, pool cancellation/failure tests | Production encryption/PITR, separately protected backups, failover, chaos/DR capacity |
| A125–128/A134–137/A142; B104/B106/B107/B138–144 | Complete requirement index/hash gate, architecture guard, pinned CI, local green gate, logical Git publication | Hosted CI verification and later milestone evidence; release signing/deployment gates |

All commercial domain features marked missing remain in scope. This addendum does
not silently promote a design, interface or mocked provider to working production support.
