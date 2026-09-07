You are working on the project:

# SalekhPos

Project path on Windows:

`C:\Users\ASUS\OneDrive\Documents\PROJECTS\SalekhPos`

This document defines the TARGET ARCHITECTURE, REPOSITORY STRUCTURE, ENGINEERING RULES, DOMAIN BOUNDARIES, PLATFORM STRATEGY, SECURITY MODEL, SCALABILITY MODEL, OFFLINE-FIRST STRATEGY, INTERNATIONALIZATION MODEL, TENANCY MODEL, TESTING MODEL, INFRASTRUCTURE MODEL, AND LONG-TERM TECHNICAL DIRECTION for SalekhPos.

Treat this document as the architectural source of truth for the project unless a newer explicit architectural decision overrides it.

Do NOT interpret this specification as an instruction to immediately create hundreds of empty directories, placeholder files, dummy classes, TODOs, or unimplemented modules.

The architecture below defines where the project is going and how every future feature must fit into the system.

Create folders, files, projects, services, abstractions, modules, infrastructure components, tests, configuration, and documentation only when they are genuinely needed for the current implementation phase.

Do not generate meaningless scaffolding purely to make the repository visually match this document.

The implementation must remain clean, compilable, testable, maintainable, production-oriented, and incrementally deliverable at every phase.

---

# 1. PRODUCT VISION

SalekhPos is not intended to be a small single-store cashier application.

It is intended to become a professional, global, multi-tenant, multi-store commercial POS and retail management platform.

The platform must be suitable for:

- very small shops
- small retailers
- medium-sized retailers
- large supermarkets
- multi-store businesses
- chains
- warehouse-based businesses
- businesses operating in multiple regions
- businesses operating in multiple countries

The architecture must not assume that every business has a large corporate staff.

A small shop may have one person acting as:

- owner
- manager
- cashier
- inventory manager

while a large company may have separate employees for every responsibility.

The permission and role model therefore needs to be flexible.

SalekhPos must support the same platform across different business sizes without requiring a separate product architecture for each type of customer.

---

# 2. TARGET PLATFORMS

SalekhPos must ultimately support:

## Web

Used primarily for:

- business owners
- managers
- administrators
- accountants
- warehouse managers
- reporting staff
- auditors
- support staff
- Super Admins
- central management

## Desktop

Target operating systems:

- Windows
- macOS
- Linux

Desktop is the primary professional POS workstation environment.

Desktop responsibilities may include:

- cashier operation
- fast sales
- barcode scanners
- receipt printers
- cash drawers
- customer displays
- weighing scales
- payment terminals
- local offline database
- offline selling
- synchronization
- shift management
- cash management
- receipt printing

The desktop POS must not require a continuous internet connection to complete ordinary sales.

## Mobile

Target platforms:

- iOS
- Android

Possible responsibilities:

- owner dashboard
- manager dashboard
- store monitoring
- sales monitoring
- inventory lookup
- stock counting
- barcode scanning
- stock receiving
- approvals
- notifications
- employee workflows
- reporting
- store management

## Future Kiosk / Self-Service

Architecture should permit a future:

- self-checkout application
- customer ordering terminal
- customer-facing kiosk

Do not build it prematurely unless required, but avoid architectural decisions that would prevent it.

---

# 3. REPOSITORY STRATEGY

Use a MONOREPO.

The repository should ultimately contain:

```text
SalekhPos/
├── apps/
├── backend/
├── packages/
├── database/
├── infra/
├── services/
├── integrations/
├── country-packs/
├── sync/
├── security/
├── tests/
├── tools/
├── docs/
├── configs/
├── localization/
├── observability/
├── scripts/
├── deploy/
├── certificates/
├── samples/
├── .github/
└── root configuration files
```

However:

DO NOT create every directory immediately.

Only materialize directories when real implementation requires them.

The repository must remain clean rather than becoming a collection of empty architecture placeholders.

---

# 4. CORE ARCHITECTURAL APPROACH

Do NOT begin SalekhPos as dozens of independently deployed microservices.

The backend should initially follow:

MODULAR MONOLITH

combined with:

- Domain-Driven Design boundaries
- clear bounded contexts
- Clean Architecture principles where useful
- CQRS where useful
- domain events
- integration events
- transactional outbox
- event-driven communication
- strong module boundaries
- module-owned data
- dependency inversion
- API versioning
- contract versioning
- asynchronous processing where appropriate

The system should be designed so that individual modules can later be extracted into independent services if scale, operational isolation, deployment independence, or organizational needs justify doing so.

Possible future extraction candidates include:

- Analytics
- Reporting
- Notifications
- Billing
- Subscription management
- synchronization
- integration processing
- import/export
- background jobs

But DO NOT split them prematurely.

Avoid distributed-system complexity unless it provides a real benefit.

---

# 5. HIGH-LEVEL TARGET REPOSITORY STRUCTURE

The target architecture is approximately:

```text
SalekhPos/
│
├── .github/
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.yml
│   │   ├── feature_request.yml
│   │   ├── security_report.yml
│   │   └── config.yml
│   ├── PULL_REQUEST_TEMPLATE.md
│   ├── CODEOWNERS
│   ├── dependabot.yml
│   └── workflows/
│       ├── ci.yml
│       ├── backend-ci.yml
│       ├── web-ci.yml
│       ├── desktop-ci.yml
│       ├── mobile-ci.yml
│       ├── integration-tests.yml
│       ├── e2e-tests.yml
│       ├── security-scan.yml
│       ├── dependency-scan.yml
│       ├── container-scan.yml
│       ├── database-migration-check.yml
│       ├── deploy-development.yml
│       ├── deploy-staging.yml
│       ├── deploy-production.yml
│       └── release.yml
│
├── .config/
├── .devcontainer/
├── .vscode/
│
├── apps/
│   ├── web/
│   ├── desktop/
│   ├── mobile/
│   └── kiosk/
│
├── backend/
│   ├── src/
│   │   ├── Bootstrapper/
│   │   ├── BuildingBlocks/
│   │   └── Modules/
│   └── tests/
│
├── packages/
├── database/
├── infra/
├── services/
├── integrations/
├── country-packs/
├── sync/
├── security/
├── tests/
├── tools/
├── docs/
├── configs/
├── localization/
├── observability/
├── scripts/
├── deploy/
├── certificates/
├── samples/
│
├── .dockerignore
├── .editorconfig
├── .env.example
├── .gitattributes
├── .gitignore
├── Directory.Build.props
├── Directory.Build.targets
├── Directory.Packages.props
├── global.json
├── package.json
├── pnpm-lock.yaml
├── pnpm-workspace.yaml
├── turbo.json
├── SalekhPos.sln
├── LICENSE
├── SECURITY.md
├── CONTRIBUTING.md
├── CHANGELOG.md
├── CODE_OF_CONDUCT.md
└── README.md
```

Again:

This is a TARGET MAP.

It is not permission to create empty scaffolding blindly.

---

# 6. WEB APPLICATION ARCHITECTURE

The target Web application structure should conceptually support:

```text
apps/web/
├── src/
│   ├── app/
│   │   ├── (public)/
│   │   │   ├── home
│   │   │   ├── pricing
│   │   │   ├── features
│   │   │   ├── contact
│   │   │   └── legal
│   │   │
│   │   ├── (auth)/
│   │   │   ├── sign-in
│   │   │   ├── sign-up
│   │   │   ├── forgot-password
│   │   │   ├── reset-password
│   │   │   ├── verify-email
│   │   │   ├── MFA
│   │   │   └── invitations
│   │   │
│   │   ├── (dashboard)/
│   │   │   ├── dashboard
│   │   │   ├── stores
│   │   │   ├── registers
│   │   │   ├── sales
│   │   │   ├── returns
│   │   │   ├── products
│   │   │   ├── categories
│   │   │   ├── inventory
│   │   │   ├── warehouses
│   │   │   ├── stock-transfers
│   │   │   ├── suppliers
│   │   │   ├── purchasing
│   │   │   ├── customers
│   │   │   ├── loyalty
│   │   │   ├── employees
│   │   │   ├── roles
│   │   │   ├── shifts
│   │   │   ├── accounting
│   │   │   ├── taxes
│   │   │   ├── reports
│   │   │   ├── analytics
│   │   │   ├── integrations
│   │   │   ├── subscriptions
│   │   │   ├── billing
│   │   │   ├── notifications
│   │   │   ├── audit-log
│   │   │   ├── security
│   │   │   └── settings
│   │   │
│   │   └── super-admin/
│   │       ├── overview
│   │       ├── tenants
│   │       ├── organizations
│   │       ├── stores
│   │       ├── users
│   │       ├── subscriptions
│   │       ├── payments
│   │       ├── plans
│   │       ├── feature-flags
│   │       ├── system-health
│   │       ├── incidents
│   │       ├── background-jobs
│   │       ├── audit
│   │       ├── security
│   │       ├── support
│   │       └── configuration
│   │
│   ├── components/
│   ├── features/
│   ├── hooks/
│   ├── lib/
│   ├── services/
│   ├── stores/
│   ├── types/
│   ├── validation/
│   ├── constants/
│   ├── config/
│   ├── i18n/
│   └── middleware/
│
├── public/
├── tests/
└── configuration files
```

Prefer feature-oriented organization where possible.

Avoid turning the frontend into giant folders such as:

```text
components/
services/
hooks/
models/
```

containing hundreds of unrelated files.

Keep domain-related logic close to its domain feature.

---

# 7. DESKTOP APPLICATION ARCHITECTURE

The desktop application must be treated as a first-class product, not a thin web wrapper.

It must support professional POS hardware and offline execution.

Conceptual structure:

```text
apps/desktop/
├── SalekhPos.Desktop/
│   ├── Views/
│   │   ├── Authentication/
│   │   ├── POS/
│   │   ├── Sales/
│   │   ├── Returns/
│   │   ├── Inventory/
│   │   ├── Customers/
│   │   ├── Shifts/
│   │   ├── CashManagement/
│   │   ├── Reports/
│   │   ├── Settings/
│   │   └── Sync/
│   ├── ViewModels/
│   ├── Controls/
│   ├── Converters/
│   ├── Behaviors/
│   ├── Styles/
│   ├── Themes/
│   ├── Assets/
│   └── Localization/
│
├── SalekhPos.Desktop.Application/
│   ├── POS/
│   ├── Authentication/
│   ├── Sales/
│   ├── Inventory/
│   ├── Shifts/
│   └── Sync/
│
├── SalekhPos.Desktop.Infrastructure/
│   ├── Api/
│   ├── LocalDatabase/
│   ├── Sync/
│   ├── Storage/
│   ├── Printing/
│   ├── Barcode/
│   ├── CashDrawer/
│   ├── Scales/
│   ├── PaymentTerminals/
│   ├── CustomerDisplay/
│   └── Hardware/
│
└── SalekhPos.Desktop.Tests/
```

Desktop must enforce separation between:

- presentation
- application logic
- local persistence
- synchronization
- hardware drivers
- cloud API communication

Do not place printer, barcode scanner, cash drawer, payment terminal, or scale-specific code directly inside screens or ViewModels.

---

# 8. MOBILE APPLICATION ARCHITECTURE

Target mobile structure:

```text
apps/mobile/
├── src/
│   ├── screens/
│   │   ├── authentication/
│   │   ├── dashboard/
│   │   ├── sales/
│   │   ├── products/
│   │   ├── inventory/
│   │   ├── stock-count/
│   │   ├── barcode-scanner/
│   │   ├── customers/
│   │   ├── employees/
│   │   ├── reports/
│   │   ├── notifications/
│   │   └── settings/
│   ├── components/
│   ├── navigation/
│   ├── features/
│   ├── services/
│   ├── hooks/
│   ├── storage/
│   ├── sync/
│   ├── permissions/
│   ├── localization/
│   ├── notifications/
│   └── security/
│
├── android/
├── ios/
├── tests/
└── configuration
```

The mobile application should share contracts, generated API clients, design tokens, localization resources, and validation rules where technically appropriate.

Do not duplicate business rules across clients when those rules belong to the backend domain.

---

# 9. BACKEND BOOTSTRAP STRUCTURE

Target:

```text
backend/src/
├── Bootstrapper/
│   ├── SalekhPos.Api/
│   │   ├── Controllers/
│   │   ├── Endpoints/
│   │   ├── Middleware/
│   │   ├── Filters/
│   │   ├── Extensions/
│   │   ├── Configuration/
│   │   ├── OpenApi/
│   │   ├── HealthChecks/
│   │   └── Program.cs
│   │
│   └── SalekhPos.Worker/
│       ├── Jobs/
│       ├── Consumers/
│       ├── Schedulers/
│       └── Program.cs
│
├── BuildingBlocks/
└── Modules/
```

The API bootstrapper must remain thin.

It should compose modules.

It must not contain the real business domain.

---

# 10. SHARED BUILDING BLOCKS

Conceptual structure:

```text
BuildingBlocks/
├── SalekhPos.SharedKernel/
│   ├── Domain/
│   │   ├── Entity
│   │   ├── AggregateRoot
│   │   ├── ValueObject
│   │   ├── DomainEvent
│   │   └── AuditableEntity
│   ├── Results/
│   ├── Exceptions/
│   ├── Primitives/
│   └── Abstractions/
│
├── SalekhPos.Application/
│   ├── Messaging/
│   ├── CQRS/
│   ├── Validation/
│   ├── Behaviors/
│   ├── Transactions/
│   └── Interfaces/
│
├── SalekhPos.Infrastructure/
│   ├── Database/
│   ├── Messaging/
│   ├── Caching/
│   ├── Security/
│   ├── Storage/
│   ├── Email/
│   ├── Sms/
│   ├── Push/
│   ├── Observability/
│   └── Resilience/
│
└── SalekhPos.Contracts/
    ├── Events/
    ├── Commands/
    ├── Queries/
    └── DTOs/
```

However:

Do not turn the SharedKernel into a dumping ground.

A component should only be shared if it is genuinely universal.

Domain-specific logic must remain inside its bounded context.

---

# 11. TARGET BACKEND MODULES

The platform should ultimately contain bounded contexts similar to:

```text
Modules/
├── Identity
├── Tenancy
├── Organizations
├── Stores
├── Authorization
├── Employees
├── Devices
│
├── Catalog
├── Pricing
├── Promotions
├── Inventory
├── Warehousing
├── Purchasing
├── Suppliers
│
├── Sales
├── Payments
├── Returns
├── CashManagement
├── ShiftManagement
│
├── Customers
├── Loyalty
│
├── Accounting
├── Taxation
├── Fiscalization
│
├── Reporting
├── Analytics
│
├── Notifications
├── Audit
│
├── Billing
├── Subscriptions
│
├── Integrations
├── Sync
│
├── Localization
├── FeatureManagement
├── Support
└── SystemAdministration
```

Do not create every module until required.

When creating new business functionality, first determine which bounded context owns it.

Do not put unrelated logic into generic folders.

---

# 12. STANDARD MODULE INTERNAL STRUCTURE

Each important backend module should approximately follow:

```text
ModuleName/
├── SalekhPos.ModuleName.Domain/
├── SalekhPos.ModuleName.Application/
├── SalekhPos.ModuleName.Infrastructure/
├── SalekhPos.ModuleName.Api/
└── SalekhPos.ModuleName.Contracts/
```

Example:

```text
Inventory/
├── SalekhPos.Inventory.Domain/
├── SalekhPos.Inventory.Application/
├── SalekhPos.Inventory.Infrastructure/
├── SalekhPos.Inventory.Api/
└── SalekhPos.Inventory.Contracts/
```

---

# 13. DOMAIN LAYER RULES

The Domain project should contain:

- aggregates
- entities
- value objects
- domain services
- domain policies
- domain events
- domain errors
- domain invariants
- business rules

It must not depend on:

- HTTP
- UI
- controllers
- database-specific APIs
- ORM-specific implementation details
- message brokers
- Redis
- cloud vendors
- payment vendors

Keep the domain model infrastructure-independent.

---

# 14. APPLICATION LAYER RULES

Application should orchestrate use cases.

Typical contents:

- commands
- queries
- command handlers
- query handlers
- validators
- application services
- authorization checks
- interfaces
- event handlers
- orchestration

Prefer feature-oriented grouping.

For example:

```text
Application/
└── Commands/
    └── ReceiveStock/
        ├── ReceiveStockCommand.cs
        ├── ReceiveStockCommandHandler.cs
        ├── ReceiveStockValidator.cs
        └── ReceiveStockResponse.cs
```

Prefer this over:

```text
Commands/
Handlers/
Validators/
Responses/
```

with the related use-case files scattered across the application.

Files that belong to one use case should remain close together.

---

# 15. INFRASTRUCTURE LAYER RULES

Infrastructure may contain:

- persistence
- ORM implementation
- repositories
- caches
- external providers
- message broker adapters
- email providers
- SMS providers
- storage implementations
- cloud adapters
- payment-provider adapters
- fiscal service adapters
- external integrations

Domain and application layers must not depend directly on vendor-specific infrastructure.

---

# 16. API LAYER RULES

The module API project may contain:

- endpoints
- HTTP request handling
- HTTP response mapping
- authentication integration
- API contracts
- API version routing
- module registration

Do not place core business rules inside controllers or endpoint handlers.

---

# 17. CONTRACTS

Contracts may contain:

- DTOs
- requests
- responses
- integration events
- stable public module contracts
- versioned event schemas

Do not expose internal domain entities directly as external API models.

---

# 18. MODULE COMMUNICATION

Modules must not freely reach into another module's database tables.

Example of BAD design:

Sales completes a sale and directly updates Inventory tables.

Preferred design:

```text
Sale Completed
      |
      v
SaleCompleted integration/domain event
      |
      +------> Inventory updates stock
      |
      +------> Loyalty awards points
      |
      +------> Reporting processes sale
      |
      +------> Accounting records financial impact
      |
      +------> Notifications processes relevant notifications
```

Use transactional outbox where required so business state changes and event publication are reliable.

Event consumers must be idempotent.

Duplicate messages must not corrupt state.

---

# 19. TENANCY MODEL

SalekhPos is MULTI-TENANT.

Conceptual hierarchy:

```text
SalekhPos Platform
|
+-- Tenant / Organization A
|   |
|   +-- Store A1
|   +-- Store A2
|   +-- Store A3
|
+-- Tenant / Organization B
|   |
|   +-- Store B1
|   +-- Store B2
|
+-- Tenant / Organization C
    |
    +-- Store C1
```

The exact terminology between Tenant and Organization must remain consistent in implementation.

Avoid unnecessary duplication where Tenant and Organization mean the same thing.

Domain objects must have correct scope.

Possible scopes include:

- platform
- tenant
- organization
- store
- warehouse
- register
- user
- device

Do not blindly add all identifiers to every table.

For example:

Not every record needs:

- TenantId
- OrganizationId
- StoreId
- WarehouseId
- RegisterId

Scope must reflect domain ownership.

However, tenant isolation must always be enforceable.

---

# 20. TENANT ISOLATION

Tenant isolation is a critical security boundary.

A user from Tenant A must never access Tenant B data because of:

- missing filters
- incorrect repository logic
- guessed identifiers
- modified HTTP requests
- frontend manipulation
- IDOR vulnerabilities
- cache leakage
- background job mistakes
- message-processing mistakes

Tenant context must be enforced server-side.

Never rely on the client to protect tenant boundaries.

Add dedicated tenant-isolation security tests.

---

# 21. AUTHENTICATION

Authentication architecture should support professional modern authentication.

Potential capabilities:

- secure password authentication
- verified email
- optional verified phone
- MFA
- TOTP
- recovery codes
- secure session management
- refresh-token rotation if token-based flows are used
- session revocation
- device/session listing
- suspicious-session detection
- future passkeys/WebAuthn
- account recovery
- brute-force protection
- rate limiting
- lockout controls

Never store plaintext passwords.

Never log authentication secrets.

---

# 22. AUTHORIZATION

Do not rely only on simple role names.

Use:

ROLE-BASED ACCESS CONTROL

combined with:

FINE-GRAINED PERMISSIONS / POLICIES.

Possible roles:

- Owner
- Store Manager
- Cashier
- Inventory Clerk
- Warehouse Clerk
- Purchasing Manager
- Accountant
- Auditor
- HR Manager
- Sales Manager
- Supervisor
- Administrator
- Support role
- custom tenant-defined roles
- Super Admin

Example permissions:

```text
sales.create
sales.view
sales.refund
sales.void

inventory.view
inventory.receive
inventory.adjust
inventory.transfer
inventory.count

products.create
products.update
products.delete
products.change_price

employees.create
employees.update
employees.assign_role

reports.view
reports.export

settings.manage

store.manage

billing.manage
```

Permissions may additionally require:

- scope
- store
- organization
- resource ownership
- transaction amount threshold
- approval
- device
- shift state

---

# 23. SUPER ADMIN SECURITY MODEL

Super Admin is not merely an ordinary tenant role.

There must be a platform-level administration model.

The initial platform must have a Root Super Admin.

The Root Super Admin is the platform owner.

Only the Root Super Admin is initially authorized to create or register additional Super Admin accounts.

No tenant user, store owner, administrator, manager, API caller, database-manipulated client, or frontend action must be able to self-promote into Super Admin.

Design approximately:

```text
SystemAdministration/
├── SuperAdmins/
│   ├── SuperAdmin
│   ├── SuperAdminRegistry
│   ├── SuperAdminInvitation
│   └── SuperAdminAudit
│
├── Commands/
│   ├── RegisterSuperAdmin
│   ├── RevokeSuperAdmin
│   └── UpdateSuperAdminPermissions
│
└── Policies/
    ├── CanRegisterSuperAdminPolicy
    └── RootAuthorityPolicy
```

Root authority must be enforced server-side.

Do not expose a normal public endpoint such as:

```text
POST /users
role=superadmin
```

that could create a Super Admin.

Super Admin creation must require privileged platform-level authorization and complete audit logging.

---

# 24. SUPER ADMIN CAPABILITIES

The Super Admin platform should eventually support:

- tenant management
- organization management
- store management
- user oversight
- subscription oversight
- billing oversight
- plan management
- feature flags
- system configuration
- incidents
- platform health
- background job visibility
- integrations
- support tools
- audit review
- security events
- abuse review
- tenant suspension
- tenant restoration
- system announcements
- country configuration
- global feature control

Critical Super Admin actions should require:

- explicit authorization
- audit logging
- reason where appropriate
- confirmation where appropriate
- possibly elevated authentication for dangerous actions

---

# 25. OFFLINE-FIRST POS

Offline support must be designed from the beginning.

Do not build a cloud-only POS and attempt to add offline mode later.

Desktop POS architecture should include:

```text
Desktop POS
|
+-- Local Database
|
+-- Local transaction state
|
+-- Local Outbox
|
+-- Sync Engine
|
+-- Conflict Resolution
|
+-- Checkpoints
|
+-- Retry Queue
|
+-- Cloud API
```

When internet connectivity disappears:

```text
Cashier
  |
  v
Sale
  |
  v
Local validation
  |
  v
Local database
  |
  v
Receipt
  |
  v
Sale completed locally
```

The cashier must be able to continue ordinary supported sales.

When internet returns:

```text
Local Outbox
  |
  v
Sync Engine
  |
  v
Cloud API
  |
  v
Server validation
  |
  v
Persist / reconcile
  |
  v
Acknowledgement / checkpoint
```

The sync engine must support:

- retries
- idempotency
- deduplication
- ordering where required
- versioned messages
- conflict detection
- conflict resolution
- checkpoints
- partial failure recovery
- safe restart
- connection interruptions
- schema evolution
- observability

Never assume requests execute exactly once.

---

# 26. OFFLINE CONFLICT STRATEGY

Different data types require different conflict policies.

Do not apply naive "last write wins" globally.

Examples:

## Sales

Completed sale records should usually be immutable financial events.

Use unique IDs and idempotency.

## Product metadata

Conflict may require version checks.

## Price updates

May need effective timestamps and versioning.

## Inventory

Inventory should preferably rely on movements/ledger entries rather than arbitrary overwrites.

## Stock count

May have explicit reconciliation workflow.

## Employee records

May use optimistic concurrency.

Build conflict behavior per domain.

---

# 27. LOCAL DATABASE

Desktop may use a local embedded database such as SQLite where technically appropriate.

The local database is not merely a cache.

For supported offline workflows it may temporarily be the authoritative local transaction store until synchronization.

Protect against:

- data corruption
- process crashes
- partial writes
- power loss
- duplicate synchronization
- concurrent operations

Use transactions appropriately.

Do not put production credentials or long-lived server secrets inside the local database.

---

# 28. HARDWARE ABSTRACTION

POS hardware must use abstraction layers.

Do NOT write:

```text
POSScreen -> Epson-specific printer code
```

Preferred:

```text
POS Application
      |
      v
IReceiptPrinter
      |
      +--> Epson implementation
      +--> Star implementation
      +--> Bixolon implementation
      +--> Generic ESC/POS implementation
```

Conceptual abstractions:

```text
IReceiptPrinter
IBarcodeScanner
ICashDrawer
IScale
IPaymentTerminal
ICustomerDisplay
```

Target folder idea:

```text
Hardware/
├── Abstractions/
├── Printers/
│   ├── Epson/
│   ├── Star/
│   ├── Bixolon/
│   └── GenericEscPos/
├── Scanners/
├── Scales/
├── CashDrawers/
├── PaymentTerminals/
└── CustomerDisplays/
```

Vendor-specific hardware must not contaminate domain logic.

---

# 29. CATALOG DOMAIN

The Catalog bounded context may ultimately support:

- products
- product variants
- categories
- brands
- SKUs
- barcodes
- units of measure
- attributes
- bundles
- kits
- service products
- images
- active/inactive state
- sales restrictions
- product metadata

Example structure:

```text
Catalog/
└── Domain/
    ├── Products/
    ├── Variants/
    ├── Categories/
    ├── Brands/
    ├── Barcodes/
    ├── Units/
    ├── Bundles/
    └── Attributes/
```

---

# 30. INVENTORY DOMAIN

Inventory should support professional stock management.

Potential concepts:

- stock level
- stock movement
- stock adjustment
- receiving
- transfers
- stock counts
- reservations
- lots
- batches
- serial numbers
- expiration
- damaged stock
- returned stock
- stock corrections
- warehouse stock
- store stock

Prefer stock movement ledgers over arbitrary direct stock-number mutation where appropriate.

Example:

```text
Inventory/
└── Domain/
    ├── Stock/
    ├── StockMovements/
    ├── StockAdjustments/
    ├── Transfers/
    ├── Counts/
    ├── Lots/
    ├── SerialNumbers/
    └── Expiration/
```

---

# 31. PURCHASING AND SUPPLIERS

The architecture should permit:

- suppliers
- supplier contacts
- purchase orders
- receiving
- partial receiving
- supplier invoices
- purchase costs
- expected delivery
- purchase returns
- supplier history
- cost changes

Do not combine purchasing logic with POS sales simply because both involve products.

---

# 32. WAREHOUSING

Large businesses may use separate warehouses.

Architecture should support:

- warehouses
- store stockrooms
- warehouse locations
- receiving
- transfers
- dispatch
- stock counting
- movement history

Small businesses should not be forced to use advanced warehouse functionality.

Features can be enabled based on business needs.

---

# 33. SALES DOMAIN

Sales may contain:

```text
Sales/
└── Domain/
    ├── Carts/
    ├── Sales/
    ├── SaleItems/
    ├── Discounts/
    ├── Taxes/
    ├── Receipts/
    └── Invoices/
```

Support future capabilities such as:

- normal sales
- discounts
- line-item discounts
- order-level discounts
- taxes
- receipts
- invoices
- suspended transactions
- resumed transactions
- voids
- refunds
- partial refunds
- sales notes
- cashier
- register
- shift linkage
- customer association

Financial transaction integrity is more important than convenience.

---

# 34. PAYMENTS DOMAIN

Payments should be separated from sales.

Potential concepts:

```text
Payments/
└── Domain/
    ├── Payment/
    ├── PaymentMethod/
    ├── Refund/
    ├── Settlement/
    └── Reconciliation/
```

Support:

- cash
- card
- mixed payment
- gift card in future
- store credit in future
- external payment providers
- refunds
- partial refunds
- reconciliation

Never store raw sensitive payment-card data unless the architecture and compliance model explicitly supports it and it is absolutely required.

Prefer payment-provider tokenization.

---

# 35. CASH MANAGEMENT

Cash management should support:

- register opening
- opening cash
- cash in
- cash out
- cash sale
- cash refund
- expected cash
- actual cash
- variance
- cash drawer operations
- closing
- approval workflows
- audit history

---

# 36. SHIFT MANAGEMENT

Possible shift concepts:

- employee
- register
- opening timestamp
- closing timestamp
- opening balance
- sales
- returns
- cash movements
- expected balance
- actual balance
- variance
- manager approval
- notes

Do not treat shift data as mere UI state.

---

# 37. RETURNS

Returns must be a proper domain workflow.

Support future cases such as:

- full return
- partial return
- return with original sale
- return without original sale if policy permits
- reason
- approval
- restocking policy
- damaged item
- refund method
- audit

---

# 38. CUSTOMER MANAGEMENT

Customers may include:

- identity/contact details
- purchase history
- loyalty membership
- store credit where supported
- notes
- consent
- communication preferences
- addresses
- tax/company information

Respect privacy requirements.

Do not expose customer information unnecessarily.

---

# 39. LOYALTY

Loyalty should be a separate bounded context if functionality becomes substantial.

Possible features:

- points
- earning rules
- redemption rules
- tiers
- rewards
- expiry
- promotions
- history

---

# 40. PRICING

Pricing must support future scenarios such as:

- base price
- store-specific price
- location-specific price
- scheduled price
- promotional price
- customer-specific price
- wholesale price
- price lists
- tax-inclusive/exclusive pricing

Do not hard-code one price field into assumptions throughout the codebase.

---

# 41. PROMOTIONS

Promotion logic may include:

- percentage discounts
- fixed discounts
- buy X get Y
- bundles
- time windows
- customer groups
- loyalty conditions
- store scope
- product/category scope

Keep pricing and promotion logic deterministic and testable.

---

# 42. ACCOUNTING

SalekhPos may not initially replace a full accounting suite.

However architecture should allow:

- sales journals
- tax totals
- payment totals
- cash reconciliation
- purchase accounting data
- financial exports
- accounting integration
- external accounting systems

Do not tightly couple POS operations to one accounting provider.

---

# 43. REPORTING

Reporting can include:

- daily sales
- monthly sales
- store performance
- cashier performance
- product sales
- category sales
- tax reports
- payment reports
- inventory valuation
- low stock
- purchasing
- profitability where supported
- returns
- shift reports

Reporting queries should not eventually overload operational transaction paths.

Prepare for future:

- read models
- materialized views
- reporting database
- analytics pipeline

but do not create unnecessary complexity before scale requires it.

---

# 44. ANALYTICS

Analytics should remain logically separate from core transaction processing.

Heavy analytics must not slow cashier transactions.

Possible future analytical capabilities:

- sales trends
- product trends
- store comparison
- customer behavior
- inventory turnover
- forecasting
- anomalies

---

# 45. AUDIT SYSTEM

Audit is a first-class platform feature.

For sensitive actions capture appropriate context such as:

- who
- what
- when
- where
- tenant
- organization
- store
- register
- device
- source IP where appropriate
- old value
- new value
- reason
- correlation ID
- request ID
- result

Example:

```text
Actor: manager_182
Action: PRODUCT_PRICE_CHANGED
Product: Coca-Cola 500ml

Previous price:
3.50 GEL

New price:
3.90 GEL

Store:
Tbilisi #4

Time:
2026-09-07T12:42:53+04:00

Device:
DESKTOP-POS-04
```

Audit is not the same as ordinary application logging.

Audit records should have stronger integrity and retention considerations.

Critical audit events should not silently disappear if normal logging fails.

---

# 46. SECURITY LOGGING VS APPLICATION LOGGING VS AUDIT

Keep these concepts distinct.

## Application logs

Used for:

- diagnostics
- exceptions
- performance
- service operation

## Security events

Used for:

- login failure
- suspicious behavior
- denied permission
- account lock
- MFA event
- suspicious token activity

## Audit events

Used for:

- business-sensitive actions
- privileged actions
- price changes
- inventory adjustment
- refund
- user-role modifications
- configuration changes
- Super Admin actions

Never log:

- passwords
- raw authentication tokens
- API keys
- secret keys
- full card data
- sensitive credentials

---

# 47. GLOBAL / INTERNATIONAL PLATFORM

SalekhPos is GLOBAL BY DESIGN.

Internationalization is not an optional later feature.

The architecture must support:

- multiple languages
- multiple currencies
- locale-aware number formatting
- locale-aware date formatting
- timezone support
- regional settings
- tax configurations
- fiscalization rules
- invoice rules
- receipt rules
- country-specific integrations
- country-specific legal requirements

Do not hard-code:

- GEL
- Georgia
- Georgian timezone
- one date format
- one decimal format
- one tax rate
- one receipt format

into core domain assumptions.

---

# 48. LOCALIZATION

Target shared localization resources:

```text
packages/localization/
├── locales/
│   ├── en/
│   ├── az/
│   ├── ka/
│   ├── tr/
│   ├── ru/
│   └── future languages
├── currencies/
├── date-formats/
├── number-formats/
└── country-config/
```

Likely initial languages may include:

- English
- Azerbaijani
- Georgian
- Turkish
- Russian

but do not design the system so these are the only possible languages.

Do not use user-visible English text directly throughout business logic.

Use translation keys.

---

# 49. TIMEZONE RULES

Store timestamps in a robust standardized form, normally UTC where appropriate.

Store location/business timezone separately.

Convert for display and local business rules.

Be careful with:

- daylight saving transitions
- local business day
- overnight shifts
- reports
- scheduled promotions
- fiscal periods

A "business day" is not always identical to a UTC date.

---

# 50. MONEY MODEL

Never use binary floating-point types such as `float` or `double` for financial monetary calculations.

Use a proper Money concept:

```text
Money
├── Amount
└── Currency
```

Examples:

```text
Money(120.50, "GEL")
Money(75.00, "USD")
Money(99.99, "EUR")
```

Use decimal-safe arithmetic.

Currency must be explicit where ambiguity could occur.

Handle:

- currency fraction digits
- rounding
- tax rounding
- line rounding
- receipt totals
- settlement totals

through defined rules.

Do not scatter arbitrary rounding throughout the code.

---

# 51. COUNTRY PACKS

Country-specific behavior should be extensible.

Target concept:

```text
country-packs/
├── core/
│   ├── CountryDefinition
│   ├── TaxDefinition
│   ├── FiscalizationDefinition
│   └── ComplianceDefinition
│
├── GE/
├── AZ/
├── TR/
├── US/
├── GB/
├── DE/
└── future countries
```

A country pack may contain:

```text
GE/
├── config/
├── taxes/
├── fiscalization/
├── receipts/
├── translations/
└── validation/
```

Avoid core code such as:

```text
if country == Georgia
else if country == Turkey
else if country == Azerbaijan
...
```

Country-specific rules should be resolved through configuration, strategy, provider, or country-pack abstractions.

---

# 52. TAXATION

Tax must be configurable and jurisdiction-aware.

Possible concerns:

- tax inclusive pricing
- tax exclusive pricing
- multiple tax rates
- tax exemptions
- product tax category
- business tax profile
- regional rules
- effective dates
- historical tax correctness

Historical transactions must not change when tax configuration changes later.

Persist enough applied-tax information with completed financial transactions.

---

# 53. FISCALIZATION

Fiscalization must be separated behind country/provider abstractions.

Possible flow:

```text
Sale
  |
  v
Fiscalization abstraction
  |
  +--> Georgia provider
  +--> another-country provider
  +--> local fiscal device
  +--> no fiscalization required
```

Do not contaminate the Sales domain with country-specific HTTP calls.

---

# 54. API VERSIONING

Plan API versioning from the beginning.

Example:

```text
/api/v1/products
/api/v1/sales
/api/v1/inventory
/api/v1/stores
```

Do not break old desktop/mobile clients casually.

Client compatibility is critical because stores may not update every terminal instantly.

Breaking API changes require:

- new version
- migration strategy
- compatibility strategy
- deprecation plan

---

# 55. EVENT VERSIONING

Integration events must also support schema evolution.

Example:

```text
SaleCompleted.v1
```

A future incompatible version may become:

```text
SaleCompleted.v2
```

Consumers must not depend on undocumented event internals.

---

# 56. IDEMPOTENCY

Idempotency is essential in distributed and offline workflows.

Apply it to important operations such as:

- sale synchronization
- payment callbacks
- webhook handling
- refunds
- background processing
- event consumers
- external integration requests where possible

Network retries must not create:

- duplicate sales
- duplicate payments
- duplicate inventory movements
- duplicate refunds

---

# 57. DATABASE STRATEGY

Use a serious relational transactional database for primary cloud OLTP data.

PostgreSQL is an appropriate direction unless an explicit architectural decision changes it.

Database architecture must support:

- migrations
- transactions
- constraints
- indexes
- optimistic concurrency where appropriate
- backups
- restoration
- high availability
- monitoring
- connection pooling
- read scaling where needed

Do not use the database merely as unvalidated storage.

Enforce critical invariants at appropriate layers.

---

# 58. MODULE-OWNED DATABASE STRUCTURE

Even while using one physical database initially, modules should logically own their schemas/data.

Example schemas:

```text
identity
tenancy
catalog
sales
inventory
billing
audit
```

A module must not casually manipulate another module's tables.

Module-owned migrations are preferred.

Example:

```text
Inventory.Infrastructure/
└── Persistence/
    └── Migrations/

Sales.Infrastructure/
└── Persistence/
    └── Migrations/
```

Avoid one giant migration directory containing unrelated changes from every domain.

---

# 59. DATABASE MIGRATIONS

Migrations must be:

- version-controlled
- deterministic
- reviewed
- tested
- safe for production
- backward-compatible where needed

CI should detect migration problems.

Never manually change production database structure without a migration process.

Potential destructive migrations require a safe rollout plan.

---

# 60. CACHE

Caching may use technology such as Redis where appropriate.

Use cache for:

- frequently accessed derived data
- distributed coordination where justified
- sessions if architecture requires it
- rate limits
- short-lived lookups
- selected read-model acceleration

Do not make critical transactional correctness depend solely on cache state.

The system must degrade safely if cache is unavailable.

---

# 61. MESSAGE BROKER

Future/production asynchronous communication may use a message broker.

Requirements:

- durable delivery where required
- retry
- dead-letter handling
- idempotent consumers
- observability
- correlation IDs
- poison-message handling

Do not introduce a broker purely because event-driven architecture sounds advanced.

Use it when operationally justified.

---

# 62. BACKGROUND JOBS

Target:

```text
services/background-jobs/
services/notification-worker/
services/report-worker/
services/sync-worker/
services/import-export-worker/
services/audit-worker/
```

Again, these may initially live in a combined worker process and only split later.

Job processing must support:

- retry
- idempotency
- cancellation where applicable
- timeout
- failure visibility
- dead-letter/failure queue
- job tracing

---

# 63. IMPORT / EXPORT

The platform should eventually support controlled import/export for:

- products
- inventory
- customers
- suppliers
- pricing
- reports

Large imports must not block normal user operations.

Use background processing where appropriate.

Validate imported data before committing it.

Provide meaningful error reporting.

---

# 64. INTEGRATIONS

Target structure:

```text
integrations/
├── payments/
├── accounting/
├── ecommerce/
├── fiscalization/
├── hardware/
└── webhooks/
```

Use abstractions before providers.

Example:

```text
payments/
├── abstraction/
├── paddle/
├── stripe/
├── adyen/
└── local-providers/
```

Provider availability depends on country and business model.

Do not make core architecture depend on one payment provider.

---

# 65. WEBHOOKS

Separate:

- incoming webhooks
- outgoing webhooks

Incoming webhook rules:

- authenticate signatures
- prevent replay where possible
- store idempotency information
- validate payload
- handle duplicates
- retry downstream processing safely

Outgoing webhook rules:

- sign payloads
- retry
- expose delivery status
- support endpoint secret rotation
- prevent one customer webhook from blocking system workers

---

# 66. BILLING AND SUBSCRIPTIONS

SaaS platform billing should be a separate domain from retail payments.

These are different concepts.

Retail Payments:

Customer pays a store.

Platform Billing:

A business customer pays SalekhPos for using the platform.

Do not mix them.

Billing/subscription domain may include:

- plans
- subscriptions
- trials
- invoices
- SaaS payments
- plan limits
- entitlement
- grace period
- cancellation
- renewal
- failed payment handling

---

# 67. FEATURE MANAGEMENT

Use a feature-management concept for:

- plan-based features
- staged rollout
- beta features
- country-specific functionality
- tenant-specific enablement
- emergency feature disablement

Do not scatter checks such as:

```text
if plan == pro
```

through the entire codebase.

Use centralized entitlements / feature policies.

---

# 68. OBSERVABILITY

Observability is a core requirement.

Support:

- structured logs
- metrics
- distributed traces
- health checks
- dashboards
- alerts
- correlation IDs
- request IDs

Target concepts:

```text
observability/
├── dashboards/
├── alerts/
├── metrics/
├── logs/
├── traces/
└── slo/
```

Potential infrastructure:

- OpenTelemetry
- Prometheus
- Grafana
- Loki
- Tempo

Technology can change, but observability architecture must remain.

---

# 69. SERVICE LEVEL OBJECTIVES

Eventually define metrics for:

- API availability
- API latency
- error rate
- synchronization latency
- payment processing
- background jobs
- database health

Example conceptual files:

```text
slo/
├── availability.yml
├── api-latency.yml
├── sync-latency.yml
└── error-rate.yml
```

---

# 70. RESILIENCE

The platform must be engineered for failure.

Assume that any external or infrastructure component can fail.

Examples:

- database transient failure
- Redis failure
- message broker failure
- external API failure
- payment provider timeout
- fiscalization provider outage
- object storage outage
- network failure
- worker restart
- server restart

Use appropriate:

- timeout
- retry with backoff
- jitter
- circuit breaker
- bulkhead
- fallback
- idempotency

Do not retry non-idempotent operations blindly.

---

# 71. GRACEFUL DEGRADATION

If a non-critical service fails, unrelated critical functions should continue where safe.

Examples:

Analytics outage should not stop cashier sales.

Email outage should not prevent recording a completed sale.

Reporting worker outage should not destroy the transaction.

External integration outage should queue supported retryable work where safe.

---

# 72. HIGH AVAILABILITY AND SCALABILITY

Design for eventual large scale.

Requirements include:

- horizontal application scaling
- stateless API nodes where practical
- distributed cache
- scalable background processing
- database high availability
- read scaling where needed
- object storage
- CDN where appropriate
- load balancing
- health-based traffic routing
- zero/low-downtime deployments
- graceful shutdown
- rolling deployment compatibility

Do not optimize prematurely, but do not make choices that make horizontal scaling impossible.

---

# 73. DISASTER RECOVERY

Plan for:

- automated backups
- tested restore procedures
- point-in-time recovery where supported
- disaster recovery documentation
- recovery objectives
- backup verification
- restore drills

Backup without restore testing is not sufficient.

Runbooks should eventually include:

```text
database-outage.md
api-outage.md
cache-outage.md
security-incident.md
rollback.md
restore-from-backup.md
```

---

# 74. SECURITY PRINCIPLES

Security must be built into the architecture.

Follow principles such as:

- least privilege
- defense in depth
- deny by default
- secure defaults
- server-side authorization
- input validation
- output encoding
- secure secret management
- encryption in transit
- encryption at rest where appropriate
- dependency security
- rate limiting
- auditability
- safe error handling
- CSRF protection where relevant
- XSS protection
- SQL injection protection
- SSRF protection
- IDOR protection
- secure file handling
- secure webhook verification

Do not expose stack traces or sensitive internal details to users.

---

# 75. SECRETS

Never commit production secrets.

Repository may contain:

```text
.env.example
```

but not:

```text
.env
```

with real secrets.

Possible configuration names:

```text
DATABASE_CONNECTION_STRING=
REDIS_CONNECTION_STRING=
MESSAGE_BROKER_CONNECTION_STRING=

JWT_ISSUER=
JWT_AUDIENCE=

OBJECT_STORAGE_ENDPOINT=

PADDLE_API_KEY=
PADDLE_WEBHOOK_SECRET=

OTEL_EXPORTER_ENDPOINT=
```

Production secrets should come from a proper secrets-management system.

Do not log them.

Do not embed them inside source code.

---

# 76. FILES THAT MUST NOT BE COMMITTED

Typical exclusions:

```text
node_modules/
bin/
obj/
dist/
build/
coverage/
.env
*.user
*.local
*.log
.vs/
.idea/
artifacts/
tmp/
secrets/
database-backups/
```

Update `.gitignore` appropriately.

---

# 77. SECURITY DOCUMENTATION

Target:

```text
security/
├── threat-model/
│   ├── STRIDE.md
│   ├── assets.md
│   ├── trust-boundaries.md
│   └── attack-surfaces.md
├── authorization/
│   ├── permissions.md
│   ├── roles.md
│   └── policies.md
├── secrets/
├── encryption/
├── authentication/
├── incident-response/
├── vulnerability-management/
└── compliance/
```

Do not create all files immediately unless useful.

But maintain threat modeling as architecture evolves.

---

# 78. TESTING STRATEGY

Tests are mandatory.

Target categories:

```text
tests/
├── unit/
├── integration/
├── contract/
├── e2e/
├── performance/
├── resilience/
├── security/
├── chaos/
├── fixtures/
└── test-data/
```

---

# 79. UNIT TESTS

Unit tests should cover:

- domain rules
- value objects
- calculations
- tax logic
- price logic
- permission policies
- stock rules
- shift calculations
- refunds
- synchronization logic
- validation

Critical financial calculations require deterministic tests.

---

# 80. INTEGRATION TESTS

Integration tests may cover:

- database
- migrations
- repositories
- cache
- message broker
- object storage
- external provider adapters
- background job infrastructure

Prefer realistic infrastructure for meaningful integration tests where practical.

---

# 81. END-TO-END TESTS

E2E tests should eventually cover important roles and workflows:

```text
e2e/
├── owner/
├── manager/
├── cashier/
├── inventory-clerk/
├── super-admin/
└── other important actors/
```

Example workflows:

- tenant registration
- owner sign-in
- create store
- add employee
- cashier login
- open shift
- sell product
- refund sale
- close shift
- receive inventory
- transfer inventory
- permission denial
- Super Admin platform action

---

# 82. SECURITY TESTS

Target:

```text
security/
├── authentication/
├── authorization/
├── tenancy-isolation/
├── injection/
├── rate-limiting/
└── abuse/
```

Important tests must verify:

- Tenant A cannot read Tenant B.
- Cashier cannot execute manager-only operations.
- Store manager cannot perform platform Super Admin operations.
- Manipulated role claims do not bypass authorization.
- Modified resource IDs do not bypass store scope.
- expired/revoked credentials are rejected.

---

# 83. PERFORMANCE TESTS

Target:

```text
performance/
├── load/
├── stress/
├── spike/
├── soak/
└── scalability/
```

Test important paths such as:

- product lookup
- barcode lookup
- sale creation
- inventory update
- login
- sync ingestion
- reporting endpoints

Do not wait until production to discover that critical POS paths collapse under concurrency.

---

# 84. RESILIENCE TESTS

Target examples:

```text
resilience/
├── database-failure/
├── cache-failure/
├── network-partition/
├── message-broker-failure/
└── service-restart/
```

Verify that retries and recovery do not cause duplicated financial operations.

---

# 85. CI/CD

GitHub Actions or equivalent CI should eventually include:

```text
.github/workflows/
├── ci.yml
├── backend-ci.yml
├── web-ci.yml
├── desktop-ci.yml
├── mobile-ci.yml
├── integration-tests.yml
├── e2e-tests.yml
├── security-scan.yml
├── dependency-scan.yml
├── container-scan.yml
├── database-migration-check.yml
├── deploy-development.yml
├── deploy-staging.yml
├── deploy-production.yml
└── release.yml
```

Pull requests should fail if mandatory checks fail.

---

# 86. INFRASTRUCTURE AS CODE

Infrastructure should eventually be reproducible.

Target:

```text
infra/
├── docker/
├── compose/
├── kubernetes/
├── terraform/
├── nginx/
├── monitoring/
└── security/
```

Terraform target modules may include:

```text
network
database
cache
messaging
compute
storage
cdn
dns
secrets
monitoring
backups
```

Environment separation:

```text
development
staging
production
```

Do not hard-code production infrastructure manually if it can be managed reproducibly.

---

# 87. DOCKER

Docker should provide consistent development/test environments.

Potential files:

```text
infra/docker/api.Dockerfile
infra/docker/worker.Dockerfile
infra/docker/web.Dockerfile

infra/compose/docker-compose.yml
infra/compose/docker-compose.dev.yml
infra/compose/docker-compose.test.yml
infra/compose/docker-compose.observability.yml
```

Avoid enormous containers with unnecessary dependencies.

Use production-safe builds.

---

# 88. KUBERNETES

Kubernetes is a future production deployment option if scale and operations justify it.

Target:

```text
kubernetes/
├── base/
└── overlays/
    ├── development/
    ├── staging/
    └── production/
```

Do not adopt Kubernetes prematurely if simpler deployment is currently more appropriate.

Architecture should remain portable.

---

# 89. SHARED PACKAGES

Target:

```text
packages/
├── api-client/
├── contracts/
├── ui/
├── localization/
├── validation/
├── logging/
├── telemetry/
├── security/
├── feature-flags/
└── testing/
```

Do not create shared packages before two or more consumers genuinely need them.

Avoid premature abstractions.

---

# 90. API CLIENT GENERATION

Where possible, generate strongly typed clients from OpenAPI or another canonical contract.

Avoid hand-maintaining the same API DTO definitions separately in:

- Web
- Mobile
- Desktop

while allowing required platform-specific wrappers.

Generated code must be kept separate from manually maintained code.

---

# 91. DESIGN SYSTEM

The platform should eventually have consistent:

- design tokens
- typography
- spacing
- iconography
- semantic colors
- component behavior
- accessibility patterns

Shared UI packages should be used where platform compatibility makes sense.

Desktop/mobile/web do not have to share the exact component implementation if framework differences make that inappropriate.

Share concepts rather than forcing bad reuse.

---

# 92. ACCESSIBILITY

UI implementations should consider:

- keyboard navigation
- screen readers
- focus management
- contrast
- touch target size
- semantic structure
- cashier speed

POS interfaces require particularly efficient keyboard and scanner workflows.

---

# 93. LOGICAL ROLE EXPERIENCE

SalekhPos should be one platform with role-sensitive interfaces.

Do not create completely separate disconnected systems for:

- cashier
- manager
- owner
- inventory worker

unless platform needs demand a separate client.

Same application may show different capabilities based on permissions.

Example:

A Cashier sees:

- POS
- current shift
- sales
- allowed returns
- customer lookup

A Manager may additionally see:

- inventory
- employees
- reports
- pricing approvals
- shift management

An Owner may see:

- all stores
- financial overview
- subscriptions
- business settings
- permissions

The Super Admin sees the entire SalekhPos platform, subject to platform security policy.

---

# 94. SMALL BUSINESS VS LARGE BUSINESS

Do not force enterprise complexity on small shops.

Architecture must support progressive complexity.

Example:

Small shop:

```text
Owner
+ Cashier
+ Inventory responsibilities
```

Large retailer:

```text
Owner
Regional Manager
Store Manager
Shift Supervisor
Cashier
Inventory Clerk
Warehouse Clerk
Purchasing Manager
Accountant
Auditor
HR Manager
```

The role system must support both.

---

# 95. DEVICE MANAGEMENT

Devices are important platform resources.

Potential device types:

- desktop POS
- mobile
- web session
- kiosk
- customer display
- payment terminal integration

Track where appropriate:

- device ID
- assigned store
- assigned register
- status
- software version
- last seen
- sync state
- trust/revocation state

Do not allow unknown/stolen POS terminals permanent unrestricted access.

---

# 96. REGISTER MODEL

A Store may contain multiple Registers.

Conceptual:

```text
Tenant
└── Store
    ├── Register 1
    ├── Register 2
    ├── Register 3
    └── Warehouse / Stock Areas
```

Registers may have:

- device assignment
- cash drawer
- printer
- payment terminal
- current shift
- register number
- status

---

# 97. DATA IMPORTANCE CLASSIFICATION

Treat data differently based on risk.

Critical:

- financial transactions
- payments
- refunds
- audit
- inventory movements
- permissions
- Super Admin actions

Important:

- product definitions
- employee configuration
- supplier information

Lower criticality:

- UI preferences
- temporary caches

Reliability strategy should reflect data importance.

---

# 98. IMMUTABILITY OF COMPLETED TRANSACTIONS

Completed financial transactions should not be freely editable.

Corrections should generally occur through new compensating actions such as:

- refund
- void where legally/operationally permitted
- adjustment
- correction transaction

Do not simply overwrite historical completed sales.

Historical reporting and auditability depend on this.

---

# 99. CONCURRENCY

Expect simultaneous operations.

Examples:

- multiple cashiers selling the same product
- two managers editing a product
- stock receiving while sales occur
- offline POS syncing while online changes happen

Use:

- database transactions
- optimistic concurrency
- version fields
- idempotency
- domain-specific conflict policies

where appropriate.

Do not assume a single user is changing the system.

---

# 100. ERROR HANDLING

Use structured application errors.

Differentiate:

- validation error
- authorization error
- authentication error
- not found
- conflict
- business rule violation
- external provider failure
- temporary infrastructure error
- unexpected internal failure

Do not expose raw internal exceptions to clients.

Clients should receive stable machine-readable error codes and safe user messages.

---

# 101. VALIDATION

Perform validation at appropriate boundaries.

Client-side validation improves UX.

Server-side validation is mandatory.

Do not trust:

- frontend
- desktop client
- mobile client
- offline client
- third-party webhook

All external input is untrusted.

---

# 102. CORRELATION

Requests, events, jobs, and sync operations should support tracing with concepts such as:

- CorrelationId
- RequestId
- CausationId
- OperationId

This is critical for diagnosing distributed workflows.

---

# 103. DOCUMENTATION

Target:

```text
docs/
├── architecture/
│   ├── overview.md
│   ├── system-context.md
│   ├── containers.md
│   ├── components.md
│   ├── module-boundaries.md
│   ├── data-flow.md
│   ├── tenancy.md
│   ├── offline-first.md
│   ├── synchronization.md
│   ├── security.md
│   ├── scalability.md
│   ├── resilience.md
│   └── disaster-recovery.md
│
├── adr/
├── api/
├── database/
├── development/
├── deployment/
├── operations/
├── security/
├── compliance/
├── testing/
├── localization/
├── integrations/
├── troubleshooting/
└── runbooks/
```

Documentation must evolve with the system.

---

# 104. ARCHITECTURAL DECISION RECORDS

Use ADRs for major decisions.

Examples:

```text
0001-monorepo.md
0002-modular-monolith.md
0003-database-strategy.md
0004-multi-tenancy.md
0005-event-driven.md
```

An ADR should explain:

- decision
- context
- alternatives
- reason
- consequences

This prevents future developers or AI agents from unknowingly undoing deliberate architecture.

---

# 105. ROOT README

README should eventually include:

- About
- Architecture
- Applications
- Requirements
- Local Development
- Docker
- Database
- Running Backend
- Running Web
- Running Desktop
- Running Mobile
- Testing
- Security
- Internationalization
- Deployment
- Repository Structure
- Contributing

Keep startup instructions accurate.

Do not allow README commands to become outdated.

---

# 106. CONFIGURATION

Configuration should be separated by environment.

Target concept:

```text
configs/
├── development/
├── testing/
├── staging/
└── production/
```

Do not allow development defaults to accidentally become insecure production defaults.

Use strongly typed configuration where supported.

Fail fast for missing critical production configuration.

---

# 107. ENVIRONMENTS

At minimum support:

- local development
- automated testing
- development
- staging
- production

Staging should resemble production enough to uncover deployment issues.

---

# 108. RELEASE STRATEGY

Because Desktop and Mobile clients may update independently from Backend:

Maintain compatibility.

Track:

- API version
- client version
- minimum supported version
- recommended version
- database schema compatibility
- sync protocol version

Do not deploy backend changes that instantly break all existing store terminals.

---

# 109. SYNC PROTOCOL VERSIONING

Target:

```text
sync/
├── protocol/
│   ├── messages/
│   ├── contracts/
│   └── versioning/
├── client/
│   ├── queue/
│   ├── conflict-resolution/
│   ├── checkpoints/
│   └── retry/
├── server/
│   ├── ingestion/
│   ├── reconciliation/
│   └── replication/
└── tests/
```

Protocol must be version-aware.

Old supported POS clients must not silently corrupt data when server schema changes.

---

# 110. DATA DELETION

Do not physically delete critical historical data merely because the UI has a Delete button.

Choose between:

- soft deletion
- deactivation
- archival
- immutable history
- hard deletion

based on domain and legal requirements.

Financial history and audit data require special care.

---

# 111. PRIVACY

Design for privacy from the beginning.

Capabilities may eventually require:

- customer consent
- data export
- data retention
- deletion/anonymization where legally permitted
- access logging
- tenant isolation

Do not collect unnecessary personal information.

---

# 112. SEARCH

Search functionality may need:

- product search
- barcode lookup
- customer search
- supplier search
- employee search
- transaction search

Do not introduce an external search engine until needed.

Start with the database if performance is sufficient.

Keep abstraction possibilities for future scale.

---

# 113. FILE / OBJECT STORAGE

Use object storage for things such as:

- product images
- exported reports
- generated documents
- authorized attachments
- backups where appropriate

Do not store large binary objects directly in the transactional database without a good reason.

Use secure URLs and permissions.

---

# 114. NOTIFICATIONS

Notification channels may include:

- in-app
- email
- push
- SMS

Use a provider abstraction.

Notification failure must not roll back unrelated completed sales.

Support retry and status tracking where needed.

---

# 115. REPORT EXPORT

Exports may support:

- CSV
- Excel
- PDF

Large exports should use background jobs.

Protect exported sensitive reports with authorization.

Temporary download files should expire.

---

# 116. OBSERVABILITY FOR OFFLINE SYNC

Track:

- queue size
- oldest unsynced transaction
- last successful sync
- failed sync count
- retry count
- conflict count
- protocol version
- client version

Owners/managers/support personnel may eventually need visibility into terminals that have not synchronized for a long time.

---

# 117. DEPLOYMENT SAFETY

Deployments should eventually use techniques such as:

- rolling deployment
- health checks
- readiness checks
- graceful shutdown
- database migration safety
- rollback
- backward-compatible messages

Never terminate a worker while it is halfway through a financial operation without safe transactional behavior.

---

# 118. DEPENDENCIES

Use well-maintained dependencies.

Avoid adding libraries when simple standard platform features are sufficient.

Before adding a major dependency consider:

- maintenance
- security
- license
- community
- release health
- long-term stability
- project lock-in

Keep dependency versions centralized where practical.

---

# 119. CODE QUALITY

Code should favor:

- clear names
- small cohesive units
- explicit business concepts
- high testability
- minimal hidden side effects
- useful abstractions
- low accidental complexity

Do not create abstractions simply to appear enterprise-grade.

Do not over-engineer trivial functionality.

But do not cut corners on:

- money
- inventory
- authorization
- tenant isolation
- audit
- sync
- payments
- security

---

# 120. NO GOD CLASSES / GOD SERVICES

Never create files such as:

```text
PosService.cs
SystemManager.cs
EverythingService.cs
CommonHelper.cs
Utils.cs
```

with hundreds or thousands of unrelated lines.

Split responsibilities according to domain.

Avoid generic "Helpers" when behavior belongs to a domain concept.

---

# 121. NO SHARED DATABASE FREE-FOR-ALL

One physical database does not mean all modules may depend on every table.

Logical module ownership must remain.

Future service extraction depends on this boundary.

---

# 122. NO FAKE MICROSERVICES

Do not create:

```text
30 tiny services
30 Dockerfiles
30 databases
30 queues
```

just because SalekhPos may become large.

Operational complexity must be justified.

Start modular.

Extract services when real scaling or operational boundaries require it.

---

# 123. NO BLIND CODE GENERATION

When implementing from this architecture:

Do not:

- generate hundreds of placeholder files
- create unused interfaces
- add fake repositories
- add empty modules
- create unused Docker services
- create unfinished API endpoints
- invent requirements
- introduce technologies merely because they sound advanced

Every implementation must serve an actual requirement.

---

# 124. CURRENT DEVELOPMENT PHILOSOPHY

Use this cycle:

```text
Architecture Target
        |
        v
Choose current vertical slice
        |
        v
Design bounded context/use case
        |
        v
Implement real functionality
        |
        v
Add tests
        |
        v
Verify build
        |
        v
Verify architecture boundaries
        |
        v
Document important decisions
        |
        v
Commit
```

Prefer complete vertical slices over large amounts of incomplete scaffolding.

---

# 125. VERTICAL SLICE EXAMPLE

A feature such as "Receive Stock" should ideally include all necessary layers:

```text
Inventory Domain
      |
      v
ReceiveStock use case
      |
      v
Validation
      |
      v
Persistence
      |
      v
API endpoint
      |
      v
Authorization
      |
      v
Audit
      |
      v
Tests
```

rather than creating ten unrelated modules that do nothing.

---

# 126. TRANSACTION BOUNDARIES

Critical operations must have explicit transactional behavior.

Examples:

Sale completion may need to atomically persist:

- sale
- sale lines
- totals
- taxes
- payment association
- audit/outbox information

Do not leave completed financial transactions in partial state.

Cross-module effects may be eventually consistent using reliable events.

---

# 127. TRANSACTIONAL OUTBOX

Where database changes must produce events, prefer an Outbox pattern.

Example:

```text
Database transaction:
    Save Sale
    Save SaleItems
    Save SaleCompleted outbox message
COMMIT

Background dispatcher:
    Read outbox
    Publish event
    Mark dispatched
```

This prevents state where a sale commits but the integration event is lost.

---

# 128. INBOX / MESSAGE DEDUPLICATION

Consumers should record or otherwise detect processed message IDs where required.

A broker retry must not apply the same inventory decrement twice.

---

# 129. INVENTORY EVENT EXAMPLE

Example flow:

```text
Sales
  |
  | SaleCompleted.v1
  v
Inventory Consumer
  |
  | idempotency check
  v
Stock movement
  |
  v
Inventory ledger update
```

Prefer a stock movement record explaining WHY stock changed.

---

# 130. BUSINESS RULE HISTORY

Where configuration affects financial results, store enough applied data on the transaction itself.

Example:

Do not calculate an old receipt tomorrow using today's tax configuration.

Store the tax that was actually applied at transaction time.

Likewise for:

- sale price
- discount
- tax
- currency
- exchange rate if relevant
- cashier
- store
- register

Historical transactions must be reproducible.

---

# 131. UNIQUE IDENTIFIERS

Use identifiers appropriate for distributed/offline creation.

Avoid relying on central sequential IDs as the only mechanism if offline clients need to create records.

Consider globally unique IDs.

Do not expose predictable IDs if they create a security risk.

Authorization must never depend on IDs being unguessable.

---

# 132. DOMAIN EVENTS VS INTEGRATION EVENTS

Keep the concepts distinct.

Domain Event:

Meaningful event inside a bounded context.

Integration Event:

Stable event intended for other modules/services.

Do not automatically publish every internal domain event publicly.

---

# 133. HEALTH CHECKS

Eventually support health endpoints for:

- API
- database
- cache
- message broker
- object storage
- critical integrations

Separate:

- liveness
- readiness

Do not report an instance ready if it cannot safely serve critical traffic.

---

# 134. RATE LIMITING

Apply sensible limits to:

- login
- password reset
- verification
- public endpoints
- expensive exports
- webhook endpoints where appropriate
- sensitive administrative actions

Internal POS traffic may have different policies from public internet endpoints.

---

# 135. SECURITY HEADERS

Web deployments should apply relevant modern security headers.

Use secure cookies where cookies are used.

Use HTTPS in production.

Do not permit insecure transport for credentials.

---

# 136. DATA ENCRYPTION

Use:

- TLS in transit
- secure password hashing
- platform/cloud encryption at rest
- field-level encryption for specially sensitive data where justified

Do not invent custom cryptography.

Use proven cryptographic libraries.

---

# 137. PAYMENT SECURITY

Payment architecture must minimize PCI scope.

Prefer certified terminals/providers.

Do not allow SalekhPos servers to receive/store raw card numbers unless an explicit compliant architecture requires it.

---

# 138. FISCAL AND LEGAL ADAPTABILITY

Country regulations change.

Therefore:

- tax rules
- fiscalization provider behavior
- receipt requirements
- invoice requirements

must be changeable without rewriting unrelated core system architecture.

---

# 139. STORE CONFIGURATION

Possible store configuration:

- name
- legal identity
- country
- currency
- timezone
- address
- receipt settings
- tax profile
- fiscal settings
- inventory policy
- pricing policy
- business hours
- enabled features

Keep global tenant settings separate from store-specific settings.

---

# 140. TENANT CONFIGURATION

Possible organization-level configuration:

- legal company info
- subscription
- default country
- default currency
- global permissions
- default tax behavior
- employee policies
- integration settings
- feature entitlements

---

# 141. PLATFORM CONFIGURATION

Platform-level configuration belongs to System Administration.

Do not mix it with tenant settings.

Only authorized platform administrators may modify it.

---

# 142. CONFIGURATION HIERARCHY

Where appropriate, configuration may follow:

```text
Platform defaults
      |
      v
Country defaults
      |
      v
Tenant settings
      |
      v
Store settings
```

But inheritance must be explicit.

Do not create hidden configuration behavior users cannot understand.

---

# 143. FEATURE FLAGS

Feature flags should not permanently replace proper configuration.

Remove obsolete flags after rollout.

Flags must have ownership and purpose.

---

# 144. UI PERMISSIONS

The UI should hide unavailable actions for usability.

But server authorization remains mandatory.

"Hiding a button" is not security.

---

# 145. SUPPORT TOOLS

Future Super Admin/support tooling may permit:

- tenant lookup
- store lookup
- user lookup
- sync diagnostics
- device diagnostics
- subscription inspection
- log correlation
- safe support actions

Avoid dangerous unrestricted database-like interfaces.

Support access should itself be audited.

---

# 146. IMPERSONATION

If platform support impersonation is ever added:

It must have:

- explicit privileged permission
- visible indication
- restricted capabilities
- reason
- complete audit log
- automatic expiration

Never implement silent impersonation.

---

# 147. DELETION / DANGEROUS ACTIONS

For destructive actions consider:

- confirmation
- reason
- stronger authorization
- approval
- audit
- reversible deactivation instead of deletion

Examples:

- deleting store
- removing business owner
- revoking Super Admin
- disabling tenant
- deleting data

---

# 148. PLATFORM MULTI-REGION FUTURE

Do not implement full multi-region infrastructure immediately.

But avoid unnecessary assumptions that all tenants permanently live in one country or timezone.

Future architecture may require:

- regional deployment
- data residency
- latency optimization
- country-specific service providers

Document any single-region assumptions.

---

# 149. CDN

Static public assets and suitable downloads may eventually use CDN.

Do not cache private tenant data publicly.

---

# 150. STORAGE RETENTION

Define retention policies for:

- logs
- audit
- exports
- backups
- temporary files
- notification records
- sync metadata

Do not retain everything forever by default.

---

# 151. DEV TOOLING

Target:

```text
tools/
├── cli/
├── database/
├── migration/
├── codegen/
├── localization/
├── seed/
├── diagnostics/
├── load-testing/
└── scripts/
```

Internal tools must not bypass normal safety unless explicitly designed as privileged administrative tools.

---

# 152. DEVELOPMENT SCRIPTS

Useful scripts may eventually include:

```text
scripts/setup.ps1
scripts/setup.sh

scripts/dev-start.ps1
scripts/dev-stop.ps1

scripts/test-all.ps1
scripts/lint-all.ps1
scripts/build-all.ps1

scripts/database-reset.ps1
scripts/database-backup.ps1
scripts/database-restore.ps1
```

Scripts must be safe and clearly distinguish development from production.

---

# 153. WINDOWS DEVELOPMENT

The primary current development environment is Windows.

PowerShell scripts should be first-class.

Do not assume Bash-only workflows.

Cross-platform support remains important where feasible.

---

# 154. GIT HYGIENE

Maintain:

- clear commits
- no secrets
- no generated dependency folders
- no local IDE noise
- no unrelated changes

Do not rewrite or delete user work without explicit reason.

Before large changes, understand the current repository state.

---

# 155. CODING AGENT BEHAVIOR

Whenever you, Codex, work on SalekhPos:

FIRST inspect the existing repository.

Do not assume this target architecture is already fully implemented.

Do not overwrite existing working architecture merely to make names match this document.

When existing implementation differs:

1. understand why
2. determine whether it violates this architecture
3. preserve working code when possible
4. refactor incrementally
5. avoid destructive rewrites without necessity

---

# 156. NO INVENTED REQUIREMENTS

If this architecture does not define a business rule, do not invent major behavior and silently encode it.

Example:

Do not decide on your own:

- refund time limits
- loyalty ratios
- tax percentages
- subscription prices
- maximum employee count
- cash variance tolerance

These are business rules/configuration that require explicit decisions.

Provide extensibility instead of arbitrary assumptions.

---

# 157. DEFAULT ENGINEERING PRIORITIES

When decisions conflict, prioritize:

1. correctness
2. financial integrity
3. security
4. tenant isolation
5. data integrity
6. reliability
7. offline continuity
8. maintainability
9. observability
10. performance
11. scalability
12. developer convenience

Do not sacrifice correctness for small performance gains.

---

# 158. CRITICAL SYSTEM PATHS

Treat these as highly critical:

- authentication
- authorization
- tenant isolation
- sale completion
- payment recording
- refund
- inventory movement
- shift closing
- cash reconciliation
- synchronization
- audit
- Super Admin actions

These deserve stronger testing and review than low-risk UI preferences.

---

# 159. TARGET FINAL SYSTEM CONCEPT

Conceptually:

```text
                         SalekhPos Platform
                                |
             +------------------+-------------------+
             |                  |                   |
             v                  v                   v
            Web              Desktop             Mobile
             |                  |                   |
             +------------------+-------------------+
                                |
                                v
                         Versioned API
                                |
                                v
                     Modular Backend Platform
                                |
         +----------+-----------+----------+----------+
         |          |                      |          |
         v          v                      v          v
      Sales      Inventory              Identity    Billing
         |          |                      |          |
         +----------+-----------+----------+----------+
                                |
                                v
                       Transactional Data
                                |
                +---------------+--------------+
                |                              |
                v                              v
          Async Processing               Observability
                |
                v
          Integrations / Events
```

Cross-cutting everywhere:

```text
Multi-Tenancy
Authorization
Audit
Security
Localization
Country Configuration
Tax/Fiscalization
Observability
Resilience
Idempotency
Versioning
Testing
```

---

# 160. FINAL ARCHITECTURAL PRINCIPLE

SalekhPos must not become:

- one gigantic application class
- one giant service layer
- one giant database free-for-all
- one country-specific POS
- one store-specific program
- one online-only cashier app
- one framework-dependent prototype
- dozens of premature microservices
- hundreds of meaningless abstractions

SalekhPos must become:

A modular, secure, global, multi-tenant, multi-store, offline-capable, highly reliable, horizontally scalable, observable and maintainable commercial retail platform.

The current architectural foundation should support eventual operation across:

- many countries
- many languages
- many currencies
- many tenants
- many stores
- many registers
- many devices
- many simultaneous employees
- very high transaction volumes

without requiring the entire platform to be rewritten.

---

# 161. IMPLEMENTATION RULE FOR CODEX

From this point forward, whenever you implement SalekhPos:

Use this specification as the TARGET ARCHITECTURE.

But implement incrementally.

Before every meaningful change:

1. inspect existing files and code
2. identify the owning bounded context
3. identify whether the feature belongs to Web, Desktop, Mobile, Backend, Infrastructure, Integration, Sync, Country Pack, or shared package
4. verify tenancy implications
5. verify authorization implications
6. verify offline implications
7. verify internationalization implications
8. verify audit implications
9. verify security implications
10. verify compatibility implications
11. implement the smallest complete production-quality vertical slice
12. add or update tests
13. build the affected project
14. run relevant tests
15. fix failures
16. update documentation if architecture changed
17. avoid unrelated changes

Do not mark implementation complete if the code does not compile or relevant tests fail, unless the failure is caused by a clearly documented external blocker.

---

# 162. VERY IMPORTANT: DO NOT BUILD THE WHOLE TREE NOW

This instruction is critical.

Do NOT respond to this document by automatically creating:

- every module
- every folder
- every class
- every service
- every Dockerfile
- every Terraform module
- every Kubernetes manifest
- every integration
- every test folder

The target tree exists to guide FUTURE implementation.

Create only structures needed by actual work.

BAD:

```text
Create 500 empty files so the repository looks enterprise-grade.
```

GOOD:

```text
Implement Identity correctly.
Then Tenancy.
Then Stores.
Then Authorization.
Then the first real product/catalog vertical slice.
Then POS/sales foundations.
...
```

Grow the repository according to real functionality.

---

# 163. ARCHITECTURAL CONSISTENCY RULE

When adding any future functionality, ask:

"Where does this belong according to the SalekhPos architecture?"

If the answer is unclear, determine the correct bounded context before adding code.

Never dump new functionality into whatever directory is easiest.

---

# 164. BACKWARD COMPATIBILITY RULE

Never casually break:

- API contracts
- persisted events
- offline sync messages
- database migrations
- supported desktop clients
- supported mobile clients

When breaking changes are unavoidable, introduce an explicit migration/versioning strategy.

---

# 165. SECURITY ESCALATION RULE

Any functionality dealing with:

- Super Admin
- authentication
- roles
- permissions
- tenant isolation
- payments
- refunds
- audit
- secrets
- encryption

must receive extra scrutiny.

Do not implement shortcuts such as:

```text
if user.role == "admin" allow everything
```

unless the actual documented authorization model explicitly requires it.

---

# 166. ROOT SUPER ADMIN RULE

The platform initially has one Root Super Admin controlled by the platform owner.

Additional Super Admins can exist in the future.

However:

ONLY the authorized Root Super Admin can initially authorize creation of additional Super Admins.

There must be no public registration route to become Super Admin.

Tenant owners must never become platform Super Admin automatically.

Store managers must never become platform Super Admin.

Tenant administrators must never become platform Super Admin.

Client-side claims are not trusted.

Super Admin elevation must be server-side, privileged, deliberate, and audited.

---

# 167. FINAL NON-NEGOTIABLE REQUIREMENTS

These architectural properties are NON-OPTIONAL:

- multi-tenant
- multi-store
- role/permission driven
- strict tenant isolation
- Root Super Admin protection
- global / multi-country
- multi-language
- multi-currency
- timezone aware
- configurable taxation
- extensible fiscalization
- Web
- Desktop for Windows/macOS/Linux
- Mobile for iOS/Android
- offline-capable professional POS
- synchronization
- hardware abstraction
- security-first
- full auditability for critical actions
- resilient
- observable
- testable
- horizontally scalable
- backup/restore capable
- failure-tolerant
- versioned APIs
- versioned sync contracts/events
- maintainable bounded contexts
- future service extraction capability

Any architectural decision that undermines these properties must be reconsidered.

---

# 168. FINAL INSTRUCTION TO CODEX

Do not take action solely because this architectural specification was provided.

Treat it as the SalekhPos architectural charter.

Remember it while working on every future task in this repository.

When I give you a specific implementation task later, implement that task in accordance with this architecture.

If the existing project contains architectural debt that conflicts with this specification, improve it incrementally without unnecessarily destroying working functionality.

Do not add functionality beyond the requested task merely because it appears somewhere in this long-term target.

Do not create fake completeness.

Build the system properly, one production-quality vertical slice at a time.

The ultimate goal is a professional SalekhPos platform that can grow from the first customer to a large global retail SaaS platform without architectural collapse.