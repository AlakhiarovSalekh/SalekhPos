# SALEKHPOS
## MASTER ENGINEERING SPECIFICATION, ARCHITECTURE CONTRACT AND IMPLEMENTATION DIRECTIVE

You are responsible for designing and implementing **SalekhPos**, a production-grade global retail operating platform.

This is not a toy application, tutorial project, proof of concept, demo POS, university project, or temporary prototype.

Treat this repository as the foundation of a real commercial SaaS platform intended to serve businesses ranging from a single small shop with one cash register to very large international retail organizations with many branches, warehouses, employees, terminals, countries, currencies, languages, integrations, and very high transaction volumes.

Your responsibility is not merely to make features appear to work.

Your responsibility is to build a system that is:

- correct;
- secure by default;
- maintainable;
- modular;
- testable;
- auditable;
- scalable;
- highly available;
- failure tolerant;
- resilient;
- offline capable where required;
- internationally usable;
- tenant isolated;
- performant;
- recoverable;
- observable;
- backwards compatible where required;
- safe to evolve;
- difficult to misuse;
- suitable for long-term professional production use.

No feature is considered complete merely because its happy path works.

Every feature must also be evaluated for:

- authentication;
- authorization;
- tenant isolation;
- scope restrictions;
- validation;
- concurrency;
- transactions;
- idempotency;
- duplicate requests;
- partial failure;
- network failure;
- retry safety;
- offline behavior;
- data loss;
- data corruption;
- auditability;
- observability;
- localization;
- timezone behavior;
- currency behavior;
- performance;
- scalability;
- backwards compatibility;
- migration safety;
- accessibility where applicable;
- security testing;
- automated testing;
- recovery.

---

# PART 1 — NON-NEGOTIABLE ENGINEERING RULES

These rules override shortcuts, convenience decisions, speculative additions and premature optimization.

## 1.1 Never knowingly sacrifice correctness for implementation speed

Development should move efficiently, but never implement a shortcut that knowingly introduces:

- data corruption;
- cross-tenant access;
- security vulnerabilities;
- broken authorization;
- irreversible migrations;
- silent failures;
- duplicate financial transactions;
- inaccurate monetary calculations;
- unsafe inventory mutation;
- uncontrolled retries;
- data loss;
- unreliable synchronization.

If a requested approach conflicts with data integrity or security, implement the safe approach.

---

## 1.2 Never claim mathematical impossibilities

Do not claim:

- that the application can never crash;
- that it can never be hacked;
- that zero bugs are guaranteed;
- that infinite users can be served by finite infrastructure;
- that data loss is mathematically impossible.

Instead engineer measurable reliability, security, recovery, redundancy, testing and scalability.

---

## 1.3 Never trust a client

Web, mobile and desktop clients are untrusted.

Never trust values supplied by clients for:

- tenant identity;
- branch scope;
- permissions;
- product price;
- tax;
- discount eligibility;
- approval state;
- payment state;
- inventory state;
- subscription entitlement;
- privileged role;
- platform ownership;
- calculated totals.

Critical decisions must be re-evaluated server-side.

---

## 1.4 Never silently lose critical data

Critical writes must not be acknowledged until the required durable persistence guarantee is achieved.

Do not show a user:

"Sale completed"

if the sale only exists in volatile memory.

Do not show:

"Payment completed"

unless payment state is known and persisted appropriately.

Do not silently discard:

- sales;
- payments;
- refunds;
- stock movements;
- purchase receipts;
- inventory transfers;
- accounting-impacting operations;
- user/security changes.

---

## 1.5 Never use floating-point values for money

Do not use binary floating point for financial calculations.

Use appropriate fixed precision decimal representation and explicit currency.

Every monetary amount must have clear currency context.

Store applied exchange rates for historical transactions where currency conversion occurred.

Historical transactions must never change because today's exchange rate changed.

---

## 1.6 Never hardcode country assumptions

Do not hardcode:

- GEL;
- USD;
- EUR;
- 18% VAT;
- US tax rules;
- Georgian tax rules;
- a particular timezone;
- one language;
- one date format;
- one decimal separator;
- one address format;
- one measurement system.

Country-specific behavior must be configurable or implemented through well-defined country/fiscal adapters.

---

## 1.7 Never implement authorization only in the UI

Hiding a button is not security.

Every privileged operation must be authorized server-side.

---

## 1.8 Never implement unrestricted queries against unbounded datasets

No production endpoint may casually retrieve an entire large table.

Use:

- pagination;
- filtering;
- appropriate indexes;
- projections;
- bounded queries;
- cursor/keyset pagination where appropriate.

Avoid N+1 queries.

Avoid full table scans on critical POS request paths.

---

## 1.9 Never create an unnecessary single point of failure

Critical production architecture must be capable of eliminating or mitigating single points of failure at the infrastructure level.

The application design must support multiple stateless API instances and multiple workers.

---

## 1.10 Never make Redis or any cache the only source of critical business data

Cache loss must not cause permanent business data loss.

---

## 1.11 Never allow unlimited retries

Retries must be:

- bounded;
- safe;
- observable;
- exponential;
- jittered where appropriate;
- used only for retryable failures.

---

## 1.12 Never perform dangerous destructive actions without audit and controls

Critical destructive operations must require appropriate authorization.

Prefer recoverable deletion where business/legal requirements permit.

---

# PART 2 — PRODUCT DEFINITION

SalekhPos is a:

**Global, multi-tenant, multi-business, multi-branch, multi-warehouse, multi-language, multi-country, multi-currency, cross-platform, offline-capable Retail Operating Platform.**

It must ultimately support:

## Web

Modern web browsers.

## Desktop

- Windows
- macOS
- Linux

## Mobile

- iOS
- Android

All platforms connect to the same SalekhPos platform and security model.

Different roles receive appropriate experiences without requiring entirely separate independent backend systems.

---

# PART 3 — BUSINESS SCALE MODEL

SalekhPos must work for:

## Small business

Examples:

- one kiosk;
- one mini-market;
- one small store;
- one branch;
- one or several terminals;
- few employees.

The application must remain simple and uncluttered for these customers.

## Medium business

Examples:

- several branches;
- several warehouses;
- dozens or hundreds of employees;
- centralized purchasing;
- centralized reporting;
- regional management.

## Large / enterprise retail

Examples:

- tens, hundreds or potentially more branches;
- many warehouses;
- regional operations;
- thousands of employees;
- large transaction volume;
- custom integrations;
- advanced security requirements;
- data residency requirements;
- enterprise identity;
- advanced audit;
- custom SLA requirements.

Do not design the UI so that small businesses are overwhelmed by enterprise functionality.

Use:

- permissions;
- plan entitlements;
- feature flags;
- configuration;
- role-specific navigation.

---

# PART 4 — HIGH-LEVEL DOMAIN HIERARCHY

Implement a flexible hierarchy broadly following:

Platform
→ Tenant / Organization
→ Business / Brand
→ Region where applicable
→ Branch
→ Warehouse / Stock Location
→ POS Terminal / Device
→ User / Employee

Do not assume every organization has only one business.

Do not assume every business has only one branch.

Do not assume warehouses always belong physically inside stores.

Do not assume a user belongs to only one branch.

A user may have scoped access to:

- one branch;
- several branches;
- a region;
- an entire business;
- multiple businesses where explicitly permitted.

---

# PART 5 — PLATFORM OWNERSHIP

There is a distinction between:

- Platform Owner
- Super Admin

The original owner account has the highest level of authority.

The design must support additional Super Admins in the future.

However:

**Only the Platform Owner may create or grant another Super Admin role unless the owner explicitly changes this policy in the future.**

A normal Super Admin must not be able to:

- remove the Platform Owner;
- demote the Platform Owner;
- become Platform Owner;
- grant Platform Owner to themselves;
- create another Super Admin unless explicitly permitted by owner-controlled policy;
- bypass security auditing.

Highly privileged operations must be strongly audited.

Platform Owner security should support:

- mandatory strong MFA;
- passkeys/security keys where supported;
- re-authentication for dangerous operations;
- session visibility;
- device visibility;
- security alerts;
- recovery protections;
- comprehensive immutable/separately protected audit records.

Do not rely merely on a visible role string such as:

role = "PlatformOwner"

for authorization.

Implement privilege boundaries properly.

---

# PART 6 — TENANT ISOLATION

Tenant isolation is one of the highest-priority security requirements.

Tenant A must never be able to access Tenant B through:

- changing URL identifiers;
- changing request bodies;
- changing query parameters;
- modifying local application state;
- forging branch identifiers;
- direct API use;
- broken cache keys;
- asynchronous jobs;
- exports;
- reports;
- search;
- notifications;
- realtime channels;
- WebSocket subscriptions;
- file storage paths.

Do not trust a client-provided TenantId.

Tenant context must come from a trusted authenticated server-side context.

Every data-access path must be evaluated for tenant isolation.

Create automated tests specifically attempting cross-tenant access.

---

# PART 7 — ROLE AND PERMISSION SYSTEM

Do not rely only on hardcoded roles.

Provide sensible default roles such as:

- Business Owner
- Regional Manager
- Branch Manager
- Assistant Manager
- Inventory Manager
- Warehouse Manager
- Warehouse Clerk
- Receiving Clerk
- Procurement Manager
- Purchasing Officer
- Cash Supervisor
- Senior Cashier
- Cashier
- Finance Manager
- Accountant
- HR Manager
- Customer Service
- Auditor
- Loss Prevention
- IT Administrator

But implement permissions independently.

Example permissions:

sales.view
sales.create
sales.refund
sales.void
sales.discount
sales.price_override

inventory.view
inventory.receive
inventory.adjust
inventory.transfer
inventory.count

catalog.view
catalog.create
catalog.edit
catalog.archive

pricing.view
pricing.manage
promotions.manage

customers.view
customers.manage

employees.view
employees.manage

reports.sales.view
reports.finance.view

settings.manage
settings.tax.manage

security.users.manage
security.roles.manage

subscription.view
subscription.manage

Permissions must support scopes such as:

- own;
- terminal;
- warehouse;
- branch;
- multiple branches;
- region;
- business;
- organization;
- platform.

Custom roles must be possible.

Example:

Night Supervisor

Permissions:

- view sales;
- close register;
- approve refunds up to configured limit;
- view inventory;

but not:

- manage employees;
- change tax configuration;
- export financial reports.

Approval thresholds must be configurable.

---

# PART 8 — SUBSCRIPTION ARCHITECTURE

Subscription logic must be designed from the start.

Do not implement subscription as:

if plan == "Premium"

Instead create an entitlement/limits system.

Marketing categories:

## Small Business

Suggested plans:

- Start
- Grow
- Plus

## Medium Business

Suggested plans:

- Standard
- Advanced
- Premium

## Large / Enterprise

Suggested plans:

- Enterprise
- Enterprise Plus
- Enterprise Custom

These names are configurable business/marketing names, not hardcoded business logic.

A plan consists of:

- entitlements;
- resource limits;
- support level;
- optional usage allowances;
- country availability;
- billing rules.

Entitlements may include:

- POS;
- inventory;
- procurement;
- loyalty;
- advanced reporting;
- advanced pricing;
- multi-branch;
- warehouse;
- API;
- custom roles;
- advanced security;
- SSO;
- enterprise audit;
- accounting integration;
- data residency;
- advanced BI.

Limits may include:

- branches;
- active terminals;
- employees;
- warehouses;
- API usage;
- cloud storage;
- optional communication allowance.

Support add-ons:

- additional terminal;
- additional branch;
- warehouse pack;
- employee pack;
- advanced analytics;
- API access;
- accounting integration;
- additional storage;
- premium support.

Subscription state machine must support:

- trial;
- active;
- grace period;
- past due;
- suspended;
- cancelled;
- expired.

Do not instantly destroy business operation because a payment temporarily failed.

Design a configurable grace-period strategy.

Downgrade must never automatically destroy existing customer data.

Example:

A customer has 15 warehouses but downgrades to a plan allowing 5.

Do not delete 10 warehouses.

Preserve data and apply safe restrictions.

Subscription prices must support:

- monthly;
- annual;
- regional price books;
- multiple billing currencies;
- tax treatment;
- discounts;
- coupons;
- promotional pricing;
- custom enterprise contracts.

Business size must not rely solely on the customer's self-description.

Consider:

- branch count;
- terminal count;
- employee count;
- warehouse count;
- transaction volume;
- required features.

The backend should remain entitlement-driven even if marketing categories change.

---

# PART 9 — INTERNATIONALIZATION AND LOCALIZATION

Internationalization is a core architectural requirement, not a future feature.

Support:

- multiple languages;
- user-specific language;
- business default language;
- branch locale;
- RTL layouts;
- Unicode;
- locale-specific numbers;
- locale-specific dates;
- locale-specific time;
- address formats;
- phone formats;
- currency formats;
- measurement units.

UI text must not be hardcoded throughout application code.

Use localization resources.

A Georgian cashier and English-speaking manager in the same store must be able to use their respective UI languages.

---

# PART 10 — TIME

Persist server-side canonical timestamps appropriately, generally UTC.

Preserve business timezone context where required.

Every branch must have an explicit timezone.

Never assume the server's local timezone is the business timezone.

DST transitions must be handled correctly.

Reporting such as:

"sales today"

must mean today in the branch/business reporting timezone, not necessarily UTC.

Audit timestamps must be unambiguous.

---

# PART 11 — CURRENCIES AND FINANCIAL CALCULATION

Support ISO-style currency identities.

A business has a base currency.

Transactions may optionally involve another currency.

Persist:

- transaction currency;
- amounts;
- conversion rate used;
- base currency equivalent where required;
- applied timestamp/source metadata where relevant.

Historical amounts must remain historically correct.

Implement deterministic rounding.

Country/business rounding policy must be configurable.

Receipt totals, payment totals and persisted totals must agree.

Do not allow hidden floating-point rounding differences.

---

# PART 12 — COUNTRY CONFIGURATION

Define a country/market configuration layer containing concepts such as:

- country;
- supported currencies;
- default currency;
- supported languages;
- default locale;
- timezones;
- tax categories;
- tax calculation behavior;
- rounding behavior;
- fiscal receipt requirements;
- address formatting;
- phone formatting;
- measurement systems;
- privacy/retention requirements;
- fiscal integration adapter;
- payment adapter availability;
- regulatory features.

Do not contaminate core Sale logic with country-specific if/else chains.

Use clean adapter/plugin boundaries where practical.

---

# PART 13 — PRODUCT CATALOG

Product domain should support professional retail requirements.

A product may include:

- unique internal identifier;
- SKU;
- name;
- localized names where applicable;
- description;
- category;
- brand;
- manufacturer;
- supplier relationships;
- tax category;
- unit of measure;
- weight;
- dimensions;
- barcodes;
- variants;
- status;
- images;
- cost references;
- pricing references;
- batch/lot tracking;
- serial tracking;
- expiry tracking.

Do not assume every product has exactly one barcode.

Do not assume every product has exactly one price.

Do not assume every product is counted only as integer units.

---

# PART 14 — PRODUCT VARIANTS

Support variants such as:

Product: Shirt

Variants:

- Black / S
- Black / M
- Black / L
- White / S
- White / M

Variants may have individual:

- SKU;
- barcode;
- price;
- cost;
- stock;
- attributes.

---

# PART 15 — BARCODE SYSTEM

Support architecture for common barcode types including where appropriate:

- EAN;
- UPC;
- Code 128;
- QR;
- internal barcodes;
- weighted barcodes;
- price-embedded barcodes.

Abstract scanning from business logic.

Allow:

- hardware scanner;
- camera scanning;
- supported external scanners.

---

# PART 16 — INVENTORY MODEL

Do not model inventory merely as:

Product.Stock = 50

Use a proper inventory ledger / stock movement model.

Examples:

+100 purchase receipt
-2 sale
+1 customer return
-3 damaged
-10 transfer out
+10 transfer in
+5 manual approved adjustment

Every stock mutation should have:

- tenant;
- business;
- location;
- product/variant;
- quantity;
- reason/type;
- source document;
- timestamp;
- actor/system source;
- correlation/reference;
- approval context where required.

Current stock may be derived/projected/materialized for performance, but movement history is crucial for traceability.

---

# PART 17 — STOCK LOCATIONS

Support logical inventory location structures such as:

Warehouse
→ Zone
→ Aisle
→ Rack
→ Shelf
→ Bin

Do not force all businesses to use the full hierarchy.

Small businesses should be able to use a simple stock location model.

---

# PART 18 — BATCH, LOT, EXPIRY AND SERIAL TRACKING

Support optional:

- batch number;
- lot number;
- manufacturing date;
- expiry date;
- supplier;
- purchase cost;
- received date.

For serialized goods support:

- serial number;
- IMEI or appropriate identifiers where applicable.

Do not force these features onto products that do not need them.

---

# PART 19 — STOCK COUNTING

Support:

- full inventory count;
- cycle count;
- blind count where configured;
- mobile barcode count;
- variance calculation;
- approval workflow.

Example:

Expected: 237
Counted: 231
Variance: -6

Do not silently change stock from the count.

Create appropriate approved stock adjustment transactions.

---

# PART 20 — PROCUREMENT

Professional procurement lifecycle should support:

Reorder suggestion
→ Purchase Request
→ Approval
→ Purchase Order
→ Supplier
→ Shipment / Expected Delivery
→ Goods Receipt
→ Inspection where configured
→ Inventory receipt
→ Supplier invoice/reference
→ Supplier return where needed

Support:

- partial deliveries;
- over/under delivery policies;
- backorders;
- purchase order status;
- cancellation;
- return to supplier.

---

# PART 21 — SUPPLIERS

Supplier profile may include:

- company information;
- contacts;
- addresses;
- currencies;
- supplied products;
- agreements;
- payment terms;
- lead times;
- purchase history;
- returns;
- performance;
- balances/invoice references where supported.

---

# PART 22 — REORDERING

Prepare architecture for reorder calculations based on:

- on-hand stock;
- available stock;
- reserved stock;
- sales velocity;
- minimum stock;
- safety stock;
- lead time;
- open purchase orders;
- seasonal behavior in future.

Automatic suggestion does not automatically imply automatic purchasing.

Approval should be configurable.

---

# PART 23 — POS CORE

POS must be one of the fastest and most resilient parts of SalekhPos.

Core flow:

Authenticate / unlock
→ Open shift/register
→ Scan/search
→ Cart
→ Customer optional
→ Discounts/promotions
→ Payment
→ Durable sale completion
→ Receipt
→ Next customer

POS should minimize unnecessary network dependencies.

Critical interactions should feel immediate.

---

# PART 24 — OFFLINE POS

Desktop and appropriate mobile POS functionality must be designed offline-first where legally and operationally possible.

When internet is unavailable, supported operations may include:

- barcode lookup;
- product lookup;
- cart;
- cash sale;
- local receipt;
- durable local queue;
- local transaction history;
- appropriate local inventory projection.

Some fiscal/payment operations may legally require connectivity.

Country/payment adapters determine what can be done offline.

Never falsely mark an online authorization as completed while offline.

---

# PART 25 — LOCAL DURABLE DATABASE

Use a reliable local transactional store such as SQLite where appropriate.

Possible local data:

- authorized product subset;
- prices;
- tax configuration;
- promotions needed for POS;
- barcode indexes;
- customer cache where permitted;
- cart recovery;
- pending transactions;
- sync outbox;
- local metadata.

Protect sensitive cached data appropriately.

Local database migration must be crash-safe and recoverable.

---

# PART 26 — SYNC ENGINE

Synchronization is a major subsystem.

Do not implement it as a simple blind "upload everything" mechanism.

Every synchronization operation should have concepts such as:

- operation ID;
- tenant;
- business;
- branch;
- terminal/device;
- entity/event type;
- payload/reference;
- local sequence;
- timestamp;
- version;
- status;
- retry count;
- last error;
- acknowledgement.

Sync must support:

- retries;
- resumability;
- duplicate protection;
- idempotency;
- partial connectivity;
- large initial sync;
- chunking;
- checkpointing;
- conflict handling;
- monitoring.

If application closes during initial sync, do not unnecessarily restart from zero.

---

# PART 27 — CONFLICT RESOLUTION

Do not use one universal conflict rule.

Different domains require different strategies.

Examples:

Inventory:
prefer ledger/events and valid movement reconciliation.

Settings:
optimistic concurrency may be appropriate.

Financial transactions:
must never be casually overwritten.

Conflicting high-risk data may require explicit reconciliation.

Implement version/concurrency metadata where necessary.

---

# PART 28 — TRANSACTIONAL OUTBOX / INBOX

For operations requiring:

database state change
+
event/message publication

avoid dual-write inconsistency.

Use an appropriate transactional outbox pattern.

Consumers requiring deduplication should implement inbox/idempotency behavior.

---

# PART 29 — CART RECOVERY

A crash must not unnecessarily destroy an in-progress cart.

Persist cart/checkpoint state appropriately.

After restart, restore or safely discard according to explicit workflow.

Do not create a completed sale merely because a cart existed.

---

# PART 30 — SALES

Sale domain should properly model:

- draft/cart where needed;
- completed sale;
- line items;
- product snapshots;
- quantity;
- unit price;
- discount;
- promotion;
- tax;
- totals;
- customer;
- cashier;
- register;
- branch;
- terminal;
- currency;
- payments;
- receipt;
- status;
- timestamps;
- audit identifiers.

Historical sale representation must not change when product name/current price changes later.

Persist necessary transaction snapshots.

---

# PART 31 — PRICING

Do not keep only Product.Price.

Support architecture for:

- base price;
- branch price;
- regional price;
- customer-group price;
- wholesale price;
- scheduled price;
- promotional price.

The server is authoritative for final price calculation.

---

# PART 32 — PROMOTIONS

Prepare promotion engine for rules such as:

- percentage discount;
- fixed discount;
- buy X get Y;
- quantity pricing;
- bundles;
- scheduled promotions;
- coupon;
- category promotion;
- loyalty-specific promotion;
- customer-group promotion.

Define deterministic conflict/stacking rules.

The same cart must not produce random totals based on rule evaluation order.

---

# PART 33 — MANUAL DISCOUNTS AND PRICE OVERRIDES

Price override and large discounts require:

- permission;
- configurable thresholds;
- optional manager approval;
- reason;
- audit record.

---

# PART 34 — PAYMENT MODEL

Core payment types may include:

- cash;
- card;
- bank/external;
- gift card;
- store credit;
- voucher;
- split payment.

Use provider adapters.

Do not tightly couple SalekhPos core business logic to one payment provider.

---

# PART 35 — PAYMENT IDEMPOTENCY

Payment operations must be designed for duplicate requests.

A network timeout may result in:

client does not know whether charge succeeded.

Never simply retry blindly and double-charge.

Persist provider references, idempotency identities and status.

Implement reconciliation behavior.

---

# PART 36 — SPLIT PAYMENTS

Support:

Total 100

- 20 cash
- 50 card
- 30 gift card

Payment sum rules must be validated.

Refund allocation must be well-defined.

---

# PART 37 — CASH MANAGEMENT

Register/shift lifecycle should support:

- opening amount;
- cash sales;
- cash refunds;
- cash in;
- cash out;
- safe drop;
- expected cash;
- counted cash;
- difference;
- closing.

Sensitive operations require permission and audit.

---

# PART 38 — SHIFTS

Support:

Employee
→ Start shift
→ POS operations
→ Cash count
→ Close shift

Handle:

- handover;
- abandoned/crashed terminal;
- forced close by authorized manager;
- discrepancy review.

---

# PART 39 — RETURNS AND REFUNDS

Support:

- receipt-based return;
- partial return;
- full return;
- exchange where implemented;
- configurable no-receipt workflow;
- reason codes;
- approvals;
- stock disposition;
- refund method.

Stock disposition examples:

- return to available stock;
- damaged;
- quarantine;
- supplier return.

Do not increase sellable stock automatically if returned goods are damaged.

---

# PART 40 — RECEIPTS

Receipt architecture should support:

- physical print;
- digital receipt;
- email;
- QR;
- PDF;
- country-specific fiscal receipt.

Separate normal receipt rendering from fiscal authority integration.

---

# PART 41 — HARDWARE ABSTRACTION

Do not spread vendor-specific hardware code throughout business logic.

Define abstractions such as:

- barcode scanner;
- receipt printer;
- cash drawer;
- weighing scale;
- customer display;
- label printer;
- card terminal;
- NFC where appropriate.

Platform/vendor implementations should be isolated.

---

# PART 42 — CUSTOMERS / CRM

Customer may include:

- name;
- identifiers;
- contacts;
- addresses;
- preferred language;
- purchase history;
- loyalty;
- store credit;
- gift cards;
- returns;
- preferences;
- consent/privacy information.

Do not collect unnecessary personal data.

Apply country/regional privacy requirements.

---

# PART 43 — LOYALTY

Prepare architecture for:

- points;
- tiers;
- rewards;
- coupons;
- membership;
- gift cards;
- store credit.

Loyalty financial value must be auditable.

Prevent duplicate redemption.

---

# PART 44 — EMPLOYEES

Employee management may include:

- user association;
- roles;
- assigned branches;
- warehouses;
- status;
- shifts;
- attendance where enabled;
- permissions;
- performance indicators.

Do not expose unnecessary personal information to roles without permission.

---

# PART 45 — APPROVAL ENGINE

Build approvals generically where practical.

Examples:

Cashier discount > threshold
→ Manager approval

Inventory adjustment > threshold
→ Inventory/Branch manager approval

Purchase Order > threshold
→ Finance
→ Owner

Support:

- requester;
- approver;
- multi-step approval;
- status;
- reason;
- expiration;
- rejection;
- audit;
- approval thresholds.

---

# PART 46 — AUDIT

Audit is a first-class subsystem.

Critical audit event should be able to answer:

- who;
- what;
- when;
- tenant;
- business;
- branch;
- device;
- session;
- before;
- after;
- reason;
- approval;
- correlation ID;
- relevant source IP metadata where lawful/useful.

Critical audit logs must not be modifiable by ordinary users.

Platform administrative access to tenant data must itself be audited.

Never log secrets.

---

# PART 47 — LOSS PREVENTION

Support future or configurable detection of:

- unusual refund rates;
- repeated voids;
- excessive manual discounts;
- suspicious inventory adjustments;
- repeated cash discrepancies;
- unusual exports;
- suspicious logins;
- abnormal sensitive actions.

Do not make irreversible accusations automatically.

Flag anomalies for review.

---

# PART 48 — REPORTING

Do not execute huge analytical workloads directly on critical POS transaction paths.

Start with appropriate:

- indexed OLTP queries;
- precomputed aggregates;
- summary tables/materialized mechanisms where appropriate;
- background report generation.

Design for later analytical separation when scale requires it.

---

# PART 49 — DASHBOARDS

Business owner dashboard may include:

- revenue;
- gross profit;
- sales count;
- returns;
- discounts;
- inventory value;
- cash information;
- top products;
- low-performing products;
- branch comparison;
- employee metrics;
- stockouts;
- shrinkage indicators.

Metrics must have defined calculation semantics.

Do not display misleading numbers.

---

# PART 50 — PLATFORM ADMINISTRATION

Platform Owner/Super Admin console should be able to manage and observe:

- tenants;
- businesses;
- branches;
- users;
- plans;
- subscriptions;
- trials;
- entitlements;
- limits;
- discounts;
- custom contracts;
- terminals;
- active sessions where appropriate;
- system health;
- API health;
- database status;
- jobs;
- sync health;
- security alerts;
- support access;
- feature flags;
- country availability.

Tenant business data access by platform support/admin must follow explicit privilege and audit rules.

---

# PART 51 — SUPPORT ACCESS

Implement controlled support access.

Support personnel must not automatically have unrestricted tenant data access.

Where support impersonation/session is necessary:

- require permission;
- record reason;
- record start/end;
- clearly indicate support mode;
- audit actions;
- apply minimum necessary access.

---

# PART 52 — BACKEND ARCHITECTURE

Prefer a **modular monolith initially** unless repository requirements already justify another structure.

Do not prematurely create dozens of microservices.

Maintain strict module boundaries so services can later be extracted if justified.

Candidate backend modules:

- Identity
- Tenancy
- Organizations
- Businesses
- Branches
- Devices/Terminals
- Employees
- Authorization
- Catalog
- Pricing
- Promotions
- Inventory
- Warehousing
- Procurement
- Suppliers
- Sales
- Payments
- Returns
- Customers
- Loyalty
- Finance
- Reporting
- Notifications
- Audit
- Sync
- Localization
- Country Configuration
- Subscriptions
- Integrations
- Platform Administration

Avoid circular module dependencies.

Business rules belong in the domain/application layers, not UI controllers.

---

# PART 53 — TECHNOLOGY DIRECTION

Use a professional supported stack.

Backend direction:

ASP.NET Core / current supported .NET LTS.

Primary transactional database:

PostgreSQL current supported stable production release.

Local client database:

SQLite or equivalent reliable embedded transactional database where appropriate.

Cache/distributed coordination:

Redis-compatible solution where justified.

Realtime:

appropriate WebSocket/SignalR mechanism.

Web:

React + TypeScript or an equivalently professional established solution if the repository already has a justified stack.

Cross-platform desktop/mobile:

prefer a framework capable of professionally supporting:

- iOS;
- Android;
- Windows;
- macOS;
- Linux;

while retaining platform-native hardware integration capability.

Flutter is an acceptable candidate.

Do not rewrite existing working architecture merely to follow this suggestion if the repository already has a stronger justified choice.

Before changing major framework decisions, inspect the repository.

---

# PART 54 — DATABASE

Primary production database is relational.

Use PostgreSQL.

Design schema intentionally.

Every table must be evaluated for:

- tenant ownership;
- business scope;
- branch scope;
- foreign keys;
- uniqueness;
- indexes;
- concurrency;
- history;
- deletion;
- retention;
- timestamps.

Do not add indexes blindly.

Use query plans and realistic workloads.

---

# PART 55 — DATABASE HIGH AVAILABILITY

Application must support production deployment with:

- primary database;
- HA standby;
- automated failover capability;
- backups;
- point-in-time recovery;
- appropriate read replicas as scale requires.

Do not couple application logic to one hostname/physical database machine in a way that prevents failover.

---

# PART 56 — DATABASE DATA PROTECTION

Use multiple independent protection layers:

- transactions;
- WAL/log-based recovery;
- replication;
- automated backups;
- point-in-time recovery;
- encrypted backups;
- geographically separated backup copies where required;
- immutable backup retention for important tiers;
- restore verification;
- retention policies.

A backup that has never been restored is not proven.

Design recurring restore drills.

Production compromise must not automatically grant the same ability to destroy all backups.

Separate security boundaries where practical.

---

# PART 57 — DELETION

Prefer soft-delete/archive for business records where appropriate.

Do not blindly soft-delete every technical table.

Hard deletion may be necessary for:

- legal retention policy;
- privacy obligations;
- temporary technical data;
- explicit lifecycle data.

Critical deletion must be authorized and auditable.

---

# PART 58 — VERSIONING AND HISTORY

Maintain meaningful history for business-sensitive entities.

Examples:

- price changes;
- tax configuration;
- role changes;
- permission changes;
- important inventory configuration;
- subscription overrides.

Do not overwrite valuable history without traceability.

---

# PART 59 — CACHE

Caching may improve:

- product lookup;
- configuration;
- permission metadata;
- reference data;
- sessions where appropriate.

But cache failure must degrade gracefully.

Cache is not authoritative for critical persistent data.

Use tenant-safe cache keys.

---

# PART 60 — MESSAGE QUEUE AND WORKERS

Heavy/asynchronous work should not block critical user requests.

Candidate asynchronous workloads:

- large imports;
- exports;
- email;
- push;
- notifications;
- report generation;
- integration events;
- sync processing;
- reconciliation;
- analytics aggregation.

Use durable queue semantics where business importance requires it.

Workers must support horizontal scaling.

Jobs must be:

- idempotent where required;
- retryable safely;
- observable;
- dead-lettered or quarantined after repeated failure.

---

# PART 61 — GRACEFUL DEGRADATION

A noncritical subsystem failing should not unnecessarily stop core POS.

Examples:

Analytics unavailable
→ POS continues.

Notification provider unavailable
→ sales continue, notification queued.

Cache unavailable
→ system may become slower but should continue when capacity allows.

Realtime unavailable
→ core REST/API operations continue.

External accounting integration unavailable
→ transaction preserved, integration retried/reconciled.

---

# PART 62 — API ARCHITECTURE

Use explicit API versioning strategy.

Do not break old supported mobile clients without migration strategy.

Use DTO/contracts rather than exposing persistence entities directly.

Validate all external input.

Use consistent error envelopes/codes.

Do not expose internal stack traces or sensitive implementation details to clients.

---

# PART 63 — IDEMPOTENCY

Critical commands must support duplicate protection.

Examples:

- create sale;
- capture payment;
- refund;
- stock transfer;
- goods receipt;
- sync event.

Idempotency key must be scoped correctly.

Persist sufficient result state to return the same logical result for replayed requests.

---

# PART 64 — CONCURRENCY

Handle concurrent updates intentionally.

Example:

Only 1 item remains.

Two cashiers attempt sale.

Do not accidentally create -1 inventory unless negative stock is explicitly configured.

Use appropriate strategies such as:

- optimistic concurrency;
- row/version checks;
- locking where required;
- ledger constraints;
- serializable logic for narrow critical operations where justified.

Do not serialize the entire system unnecessarily.

---

# PART 65 — AUTHENTICATION

Support secure authentication architecture.

Requirements should include:

- strong password hashing;
- secure password reset;
- short-lived access tokens where tokens are used;
- refresh token rotation/revocation;
- session visibility;
- device tracking;
- logout/revoke;
- compromised-session handling.

Never store plaintext passwords.

Never log password/reset secrets.

---

# PART 66 — MFA AND PASSKEYS

Support strong MFA.

Prefer:

- TOTP authenticator;
- passkeys;
- hardware/security keys where appropriate;
- recovery codes.

SMS may be offered where necessary but should not be treated as the strongest authentication factor.

Platform Owner should require especially strong authentication.

---

# PART 67 — AUTHORIZATION

Authorization must be checked server-side for every protected request.

Use both:

- role/permissions;
- resource scope.

A user may have:

inventory.view

but only for:

Branch A.

They must not access Branch B simply because they possess inventory.view.

---

# PART 68 — SECURITY CONTROLS

Apply defenses appropriate to the technology and endpoint including:

- injection prevention;
- XSS prevention;
- CSRF protection where relevant;
- SSRF controls;
- secure headers;
- rate limiting;
- brute-force protection;
- input validation;
- output encoding;
- least privilege;
- secure cookie configuration where used;
- secure CORS policy;
- request-size limits;
- file upload validation;
- secrets management;
- encryption in transit;
- encryption at rest.

Do not cargo-cult security middleware.

Configure it correctly.

---

# PART 69 — SECRET MANAGEMENT

Production secrets must not live in:

- source code;
- Git;
- committed .env files;
- public logs.

Use a proper secret management mechanism.

Support secret rotation.

Use separate credentials for:

- app runtime;
- migrations;
- backups;
- administration where appropriate.

---

# PART 70 — DATA CLASSIFICATION

Define data classes such as:

- public;
- internal;
- confidential;
- sensitive;
- highly sensitive.

Logging/redaction and access decisions should reflect classification.

Never log:

- passwords;
- full secrets;
- refresh tokens;
- unnecessary sensitive customer data;
- full payment secrets.

---

# PART 71 — FILE/OBJECT STORAGE

Do not store large product images/documents in the main relational database unless strongly justified.

Use object storage for:

- product media;
- large exports;
- documents;
- report artifacts.

Protect access by tenant.

Use signed/authorized access where appropriate.

---

# PART 72 — GLOBAL INFRASTRUCTURE

Design application so global expansion is possible.

Possible future model:

Global DNS / traffic management
→ edge/CDN
→ nearest or assigned application region
→ regional application cluster
→ region-associated data services

Do not deploy multiple regions prematurely merely for architecture theatre.

But avoid choices that make future multi-region support impossible.

---

# PART 73 — DATA RESIDENCY

Allow architecture to associate tenant/business with a data region.

Examples:

- EU;
- North America;
- APAC;
- other regulated regions.

Do not promise unsupported residency automatically.

Model it so enterprise/regulatory requirements can be implemented.

---

# PART 74 — CLIENT APPLICATION STRATEGY

The product must support:

Web:
management/admin/business functions.

Desktop:
Windows/macOS/Linux professional POS and management functions.

Mobile:
iOS/Android management, inventory and appropriately configured POS functions.

Maximize reusable code where sensible.

Do not force all platforms through an abstraction that prevents reliable hardware integration.

Platform-specific adapters are acceptable and expected.

---

# PART 75 — DESKTOP HARDWARE

Desktop architecture should support integration with appropriate:

- barcode scanners;
- receipt printers;
- cash drawers;
- weighing scales;
- customer displays;
- label printers;
- card terminals;
- fiscal devices.

Use clean interfaces/adapters.

---

# PART 76 — DEVICE MANAGEMENT

Devices/terminals are first-class entities.

Track:

- device ID;
- tenant;
- business;
- branch;
- terminal;
- platform;
- app version;
- registration status;
- last seen;
- sync state;
- trust/revocation status.

Owner/admin should be able to revoke lost/compromised devices.

---

# PART 77 — TERMINAL PROVISIONING

New POS terminal onboarding should follow a controlled process:

install
→ authenticate/activate
→ choose permitted business/branch
→ register terminal
→ establish device identity/trust
→ initial configuration
→ initial data sync
→ ready

A copied local database must not automatically turn another computer into a trusted terminal.

---

# PART 78 — INITIAL SYNC

Large stores may have huge catalogs.

Initial sync must be:

- chunked;
- resumable;
- observable;
- cancellable safely;
- memory efficient;
- progress aware.

Do not freeze UI while syncing 100,000+ products.

---

# PART 79 — CLIENT PERFORMANCE

UI must remain responsive.

Do not:

- perform heavy DB calls on UI thread;
- parse massive datasets synchronously on UI thread;
- render tens of thousands of rows without virtualization/pagination;
- block POS interaction on unnecessary analytics requests.

---

# PART 80 — PERFORMANCE TARGETS

Do not merely say "fast".

Create measurable SLOs/performance budgets for critical operations.

At minimum measure:

- barcode lookup;
- add-to-cart;
- checkout;
- normal API latency;
- search;
- dashboard load;
- sync throughput;
- job backlog;
- database query latency.

Targets must be realistic and benchmarked.

---

# PART 81 — SCALABILITY

Application backend should be horizontally scalable.

Prefer stateless API instances.

Example:

Load Balancer
→ API 1
→ API 2
→ API N

Workers should also scale horizontally.

Capacity must be demonstrated by load testing.

Do not use vague statements such as:

"supports unlimited users."

Instead characterize supported load under defined infrastructure.

---

# PART 82 — NO SINGLE POINT OF FAILURE

Production design should be capable of redundant:

- API;
- worker;
- DB;
- cache;
- queue;
- storage;
- routing;

according to the chosen deployment tier.

Failure of one API instance must not stop the platform.

---

# PART 83 — OBSERVABILITY

Implement:

- structured logs;
- metrics;
- distributed tracing;
- correlation IDs;
- health checks;
- alerts.

Important dimensions include:

- tenant ID where safe;
- service/module;
- endpoint;
- duration;
- outcome;
- error class;
- trace/correlation ID.

Never leak secrets through observability.

---

# PART 84 — METRICS

Monitor at least:

- request rate;
- latency;
- error rate;
- CPU;
- memory;
- database latency;
- DB connection usage;
- replication lag;
- lock/contention;
- cache hit rate;
- queue backlog;
- job failures;
- sync backlog;
- payment failures;
- authentication failures;
- active terminal state.

---

# PART 85 — ALERTS

Potential alerts include:

- elevated API error rate;
- increased latency;
- failed database failover;
- replica lag;
- failed backup;
- failed restore test;
- disk/storage capacity;
- queue backlog;
- worker failures;
- suspicious login behavior;
- payment failures;
- sync failures;
- terminal fleet version issue.

Avoid alert spam.

Use meaningful thresholds and severity.

---

# PART 86 — HEALTH CHECKS

Implement appropriate health checks for:

- application;
- database;
- cache;
- queue;
- object storage;
- workers;
- critical external dependencies.

Separate readiness from liveness where appropriate.

Do not make an unhealthy optional external API remove the entire POS API from service if core operations can still function.

---

# PART 87 — DEPLOYMENT

Support safe deployment strategy such as:

- rolling;
- canary;
- blue/green;

depending on infrastructure.

New release must have health verification.

Rollback must be possible when safe.

Do not manually modify production servers as normal deployment process.

---

# PART 88 — DATABASE MIGRATIONS

Production migrations must be safe under large tables and live traffic.

Use expand-and-contract when needed.

Avoid:

- long table locks;
- destructive one-step schema replacements;
- code that assumes migration and deployment happen atomically.

Migration must have rollback/recovery plan where possible.

Before dangerous migration:

- assess impact;
- verify backup/recovery;
- test against production-like dataset.

---

# PART 89 — FEATURE FLAGS

Support feature flags for risky or gradual functionality.

Feature flag must not become a substitute for authorization.

Flags may support:

- tenant;
- country;
- plan;
- percentage rollout;
- internal users.

---

# PART 90 — RELEASE ENVIRONMENTS

At minimum maintain logical separation:

- development;
- test;
- staging;
- production.

Production must not be the testing environment.

---

# PART 91 — TESTING REQUIREMENTS

No major feature is complete without appropriate automated tests.

Use combinations of:

- unit tests;
- integration tests;
- database tests;
- contract tests;
- authorization tests;
- tenant-isolation tests;
- end-to-end tests;
- offline tests;
- synchronization tests;
- migration tests;
- payment-idempotency tests;
- load tests;
- stress tests;
- recovery tests;
- hardware integration tests where practical.

---

# PART 92 — SECURITY TESTING

Explicitly test:

- cross-tenant access;
- horizontal privilege escalation;
- vertical privilege escalation;
- IDOR/BOLA style access;
- broken permission scopes;
- forged tenant/branch IDs;
- duplicate payment requests;
- malicious input;
- brute force;
- token/session revocation;
- file upload attacks where applicable;
- sensitive logging.

---

# PART 93 — LOAD TESTING

Test realistic scenarios.

Do not only hammer one trivial endpoint.

Include workload mixtures such as:

- login;
- barcode lookup;
- sale;
- inventory update;
- dashboard;
- synchronization;
- report generation;
- employee actions.

Increase concurrency progressively.

Capture bottlenecks.

Fix root causes instead of merely increasing timeouts.

---

# PART 94 — CHAOS AND FAILURE TESTING

In safe non-production or controlled environments simulate:

- API instance crash;
- worker crash;
- database failover;
- cache unavailable;
- queue delay;
- external API timeout;
- network latency;
- packet loss;
- client offline;
- sync duplicate;
- application restart;
- storage degradation.

Verify graceful behavior.

---

# PART 95 — DISASTER RECOVERY

Create documented disaster recovery procedures.

Account for scenarios such as:

- primary database failure;
- region failure;
- corrupted deployment;
- accidental deletion;
- compromised credentials;
- ransomware-style destructive event;
- backup restoration.

Define measurable RPO and RTO targets by data criticality.

Sales/payment/inventory financial data should target extremely low RPO and appropriate RTO.

Do not claim zero RPO unless infrastructure actually provides the required synchronous guarantees.

---

# PART 96 — BACKUP VALIDATION

Backups must be:

- encrypted;
- monitored;
- retained;
- protected;
- periodically restored in test environment.

Restore validation should verify:

- database starts;
- expected schemas exist;
- integrity checks;
- application connectivity;
- critical business smoke tests.

---

# PART 97 — IMPORT

Large CSV/spreadsheet imports should follow:

upload
→ validate
→ preview/errors
→ background processing
→ progress
→ result

Do not perform giant imports in a single fragile web request.

Import must be tenant-safe.

---

# PART 98 — EXPORT

Large exports should be asynchronous.

Use authorized short-lived download access.

Do not let one report exhaust production memory.

---

# PART 99 — NOTIFICATIONS

Notification engine should support channels such as:

- in-app;
- push;
- email;
- SMS adapter;
- webhook.

Events may include:

- low stock;
- approval request;
- security event;
- large refund;
- cash discrepancy;
- expiring products;
- failed payment;
- sync issue.

Notification delivery failure must not roll back a successful sale.

---

# PART 100 — INTEGRATIONS

Build an integration boundary for:

- accounting;
- ERP;
- e-commerce;
- payment;
- fiscal/tax;
- shipping;
- supplier;
- CRM;
- BI;
- marketplaces.

Support:

- API;
- webhooks;
- adapters;
- asynchronous events.

Keep core business model independent from one external vendor.

---

# PART 101 — ACCOUNTING / FINANCE

SalekhPos should track enough financial structure to support professional reporting and integrations.

Potential concepts:

- sales revenue;
- taxes;
- discounts;
- COGS;
- gross profit;
- expenses where implemented;
- supplier payable references;
- customer credit/store credit;
- cash movements;
- payment settlements.

Do not attempt to fake legally compliant full accounting for every country without proper country-specific requirements.

Keep a clean integration/export boundary for professional accounting systems.

---

# PART 102 — PRIVACY AND RETENTION

Provide a framework for:

- consent;
- data retention;
- export;
- anonymization;
- deletion workflow;
- legal holds where applicable.

Regional rules may differ.

Do not blindly delete financial/legal records because a generic customer profile delete action was requested.

Separate personal identifiers from required business transaction records where appropriate.

---

# PART 103 — REPOSITORY STRUCTURE

Use a clean repository structure.

A possible monorepo:

SalekhPos/
  apps/
    web/
    client/
  backend/
    api/
    workers/
    modules/
  packages/
    contracts/
    localization/
    shared/
  infrastructure/
  docs/
  tests/
  tools/

Actual structure should match the chosen frameworks and repository reality.

Do not create arbitrary nesting merely to copy this example.

---

# PART 104 — ARCHITECTURE DOCUMENTATION

Maintain documentation alongside code.

Required documentation should eventually include:

- system overview;
- domain model;
- module boundaries;
- authentication;
- authorization;
- permission matrix;
- tenant isolation;
- database architecture;
- offline architecture;
- sync protocol;
- payment flow;
- inventory ledger;
- audit architecture;
- subscription entitlements;
- localization;
- deployment;
- backup;
- restore;
- disaster recovery;
- monitoring;
- runbooks;
- integrations.

Create ADRs for significant architectural decisions.

---

# PART 105 — API DOCUMENTATION

Maintain machine-readable API documentation such as OpenAPI.

Document:

- authentication;
- permissions;
- request;
- response;
- errors;
- idempotency;
- pagination;
- versioning.

---

# PART 106 — CODE QUALITY

Use strict code quality rules.

CI should include appropriate:

- build;
- formatting;
- lint/static analysis;
- unit tests;
- integration tests;
- dependency vulnerability scanning;
- secret scanning;
- migration validation;
- architecture checks.

Do not merge/build production artifacts when critical checks fail.

---

# PART 107 — DEPENDENCIES

Do not add a dependency merely because it saves a few lines.

Before adding important packages evaluate:

- maintenance;
- security;
- license;
- ecosystem maturity;
- necessity;
- platform support.

Pin versions/reproducibility appropriately.

---

# PART 108 — PRODUCTION ACCESS

Use least privilege.

Normal developers should not habitually perform direct production DB mutations.

Emergency/break-glass access must be:

- limited;
- authenticated strongly;
- audited;
- justified.

---

# PART 109 — LOGGING

Use structured logging.

Logs should help investigate:

- errors;
- transaction failures;
- security events;
- sync problems;
- performance.

Do not log entire sensitive objects by default.

Sanitize and redact.

---

# PART 110 — ERROR HANDLING

Do not swallow exceptions silently.

Classify failures.

Clients should receive safe user-oriented errors.

Logs/traces should preserve enough technical context.

Use global exception handling but do not turn every failure into HTTP 500 without semantics.

---

# PART 111 — USER EXPERIENCE DURING FAILURE

User must know what happened.

Examples:

If payment status is uncertain:

Do not say "Failed, try again" if retry could double-charge.

Say appropriately that status is being verified or requires reconciliation.

If offline sale is safely queued:

Clearly show:

"Saved locally / pending synchronization"

rather than pretending cloud sync succeeded.

---

# PART 112 — ACCESSIBILITY

Web/mobile/desktop interfaces should use professional accessibility practices where supported:

- keyboard navigation;
- semantic controls;
- sufficient labels;
- screen reader semantics;
- scalable text;
- focus states.

POS must support efficient keyboard-driven workflow.

---

# PART 113 — UI DESIGN PRINCIPLES

Different roles should not see unnecessary complexity.

Small-business cashier UI:

fast and minimal.

Owner dashboard:

business-oriented.

Warehouse:

inventory-oriented.

Platform administration:

separate high-privilege experience.

Avoid giant all-purpose screens.

---

# PART 114 — CONFIGURATION HIERARCHY

Support configuration hierarchy similar to:

Platform Default
→ Country Default
→ Organization
→ Business
→ Branch
→ Terminal

Only allow overrides that are safe and valid.

Example:

country legally required fiscal setting may not be overrideable at branch level.

---

# PART 115 — FEATURE AVAILABILITY

Feature availability may depend on:

User permission
AND
Plan entitlement
AND
Country support
AND
Business configuration
AND
Device/platform capability

Do not confuse these concepts.

Example:

A plan may include fiscal integration but the current country adapter may not support it.

---

# PART 116 — ENTITY IDENTIFIERS

Use globally safe identifiers for externally exposed entities.

Do not rely on predictable integer IDs for security.

Identifiers themselves are not authorization.

Always authorize the referenced resource.

---

# PART 117 — DOMAIN EVENTS

Use explicit domain events where valuable:

- SaleCompleted;
- PaymentCaptured;
- RefundCompleted;
- StockAdjusted;
- GoodsReceived;
- EmployeeCreated;
- BranchCreated.

Do not create an uncontrolled event spaghetti architecture.

Events must have clear owners and contracts.

---

# PART 118 — STATE MACHINES

For complex business entities define legal state transitions.

Examples:

PurchaseOrder:

Draft
→ Submitted
→ Approved
→ Sent
→ PartiallyReceived
→ Received
→ Closed

Prevent illegal transitions.

Likewise define clear transitions for:

- subscription;
- payment;
- refund;
- sale;
- transfer;
- approval;
- shift.

---

# PART 119 — PAYMENT/SALE CONSISTENCY

Sale, payment and inventory are related but not identical.

Do not tightly bundle external payment provider calls inside a database transaction that remains open over the network.

Design orchestration safely.

Persist intermediate state.

Support reconciliation.

---

# PART 120 — INVENTORY CONSISTENCY

Inventory must handle:

- concurrent sales;
- offline sales;
- transfers;
- receiving;
- returns;
- damaged goods;
- corrections.

Define the authoritative meaning of:

- on hand;
- available;
- reserved;
- in transit;
- damaged;
- quarantine.

Do not use one ambiguous "stock" number everywhere.

---

# PART 121 — SALES SNAPSHOT

For historical accuracy persist the necessary sale-line snapshot such as:

- product description at sale time;
- SKU;
- tax;
- unit price;
- discount;
- promotion;
- relevant identifiers.

Changing the catalog later must not rewrite historical sale reality.

---

# PART 122 — TAX CALCULATION

Tax engine must have deterministic rules.

Define:

- tax inclusive/exclusive;
- line-level/order-level rounding;
- multiple tax components if needed;
- exemptions;
- returns;
- tax snapshots.

Country adapters may alter rules.

Do not hardcode one VAT model.

---

# PART 123 — SEARCH

Start with appropriate relational/indexed search when sufficient.

At much larger scale allow dedicated search infrastructure.

Search must remain tenant-safe.

Never leak other tenant names/products through autocomplete.

---

# PART 124 — ARCHIVAL

Large historical data may eventually require archival/partition strategies.

Design retention and partitioning based on real access patterns.

Do not prematurely shard without measurements.

---

# PART 125 — SCALING DATABASE

Scale deliberately:

1. correct schema;
2. efficient queries;
3. indexes;
4. connection pooling;
5. caching;
6. read replicas;
7. partitioning;
8. workload separation;
9. only then consider more complex distribution/sharding when justified.

Do not jump directly to sharding.

---

# PART 126 — REPORTING ISOLATION

Long analytical queries must not starve POS transactions.

Apply:

- replicas;
- aggregates;
- job queues;
- analytical stores

as scale warrants.

---

# PART 127 — FEATURE MODULARITY

Modules should fail independently where possible.

Example:

Loyalty system outage should not prevent an authorized cash sale unless business configuration explicitly requires loyalty validation.

Analytics outage should never prevent checkout.

---

# PART 128 — MOBILE APP VERSIONING

Mobile app store review causes version lag.

Backend must support an explicit compatibility window.

Return clear update-required errors only when client version is genuinely unsupported.

---

# PART 129 — DESKTOP UPDATE STRATEGY

Desktop updates should support:

- signed artifacts;
- integrity verification;
- staged rollout;
- release channels;
- rollback/recovery;
- compatibility checks.

A faulty update must not be pushed instantly to every retail terminal without staged validation.

---

# PART 130 — SIGNING AND SUPPLY CHAIN

Release artifacts should be signed where platform permits.

Protect CI/CD secrets.

Use dependency and supply-chain scanning.

Do not allow arbitrary unsigned updater payload execution.

---

# PART 131 — SECURITY INCIDENT PREPARATION

Document response procedures for:

- compromised user;
- compromised admin;
- leaked credential;
- suspicious API behavior;
- malicious integration;
- data exposure.

Provide mechanisms to:

- revoke tokens;
- disable device;
- disable user;
- rotate secrets;
- disable integration;
- inspect audit trail.

---

# PART 132 — BUSINESS CONTINUITY

Core retail workflows should remain available during partial outages where safely possible.

Examples:

Internet outage:
local supported POS continues.

Notification outage:
sales continue.

Reporting outage:
checkout continues.

Temporary cloud outage:
offline-capable terminals retain durable operations.

---

# PART 133 — DATA INTEGRITY CONSTRAINTS

Enforce important invariants as close to the authoritative data layer as practical.

Do not rely solely on frontend validation.

Examples:

- unique identifiers;
- valid foreign relationships;
- non-impossible status transitions;
- money/currency consistency;
- duplicate idempotency identity;
- tenant ownership.

---

# PART 134 — SUPPORT SMALL BUSINESSES WITHOUT DUPLICATING PLATFORM

Do not build a separate "small-store application".

Use the same core platform with:

- simpler defaults;
- smaller plans;
- reduced navigation;
- optional modules.

Likewise medium/enterprise should enable more modules without forcing separate codebases.

---

# PART 135 — PLAN GROWTH

A customer should be able to grow:

Small Start
→ Small Grow
→ Small Plus
→ Medium Standard
→ Medium Advanced
→ Medium Premium
→ Enterprise

without migrating to a completely different product.

Data remains intact.

Capabilities expand.

---

# PART 136 — ENTERPRISE EXTENSIONS

Prepare architecture for future enterprise features such as:

- SSO;
- SCIM;
- custom identity providers;
- dedicated infrastructure;
- custom retention;
- private networking;
- advanced audit export;
- SIEM integration;
- custom SLA;
- custom support;
- regional data placement.

Do not implement all prematurely unless currently required.

But avoid architectural choices that prohibit them.

---

# PART 137 — DO NOT OVERENGINEER

Enterprise quality does not mean maximum complexity.

Do not introduce:

- Kubernetes;
- microservices;
- event sourcing;
- CQRS everywhere;
- service mesh;
- dozens of databases;

merely because they sound enterprise.

Adopt technology when a concrete requirement justifies it.

The initial architecture should favor clear modularity and operational simplicity while remaining scalable.

---

# PART 138 — CODEX WORKING PROCEDURE

You must not begin by blindly generating hundreds of files.

First inspect the entire existing repository.

Before implementation:

1. inspect directory structure;
2. inspect current source code;
3. inspect configuration;
4. inspect package/project files;
5. inspect database/migrations;
6. inspect tests;
7. inspect Docker/infrastructure;
8. inspect CI/CD;
9. inspect Git state;
10. identify what already exists;
11. identify what is reusable;
12. identify architectural conflicts;
13. identify security risks;
14. identify incomplete code;
15. identify placeholder/mock implementations.

Do not delete or rewrite working code without a reason.

---

# PART 139 — FIRST REQUIRED OUTPUT FROM CODEX

Before making major architectural changes, create/update documentation containing:

## Current State Assessment

- what currently exists;
- what works;
- what is missing;
- current stack;
- current risks.

## Target Architecture

- modules;
- clients;
- data stores;
- communication;
- offline;
- sync;
- security;
- deployment.

## Gap Analysis

Map current state → target state.

## Implementation Roadmap

Break development into safe phases.

Do not attempt the entire platform in one uncontrolled code generation step.

---

# PART 140 — IMPLEMENTATION ORDER

Unless the existing repository dictates a safer ordering, use approximately this sequence.

### Phase 0 — Repository stabilization

- build;
- existing tests;
- formatting;
- environment configuration;
- secret hygiene;
- basic CI.

### Phase 1 — Solution architecture

- module boundaries;
- shared contracts;
- error handling;
- validation;
- observability foundation.

### Phase 2 — Identity and tenancy

- authentication;
- users;
- sessions;
- tenant;
- organization;
- business;
- branch;
- authorization;
- permissions;
- Platform Owner.

### Phase 3 — Global configuration

- countries;
- currencies;
- localization;
- timezone;
- tax framework;
- regional settings.

### Phase 4 — Catalog

- products;
- variants;
- barcodes;
- categories;
- units.

### Phase 5 — Inventory

- stock locations;
- ledger;
- movements;
- batch;
- serial;
- counts.

### Phase 6 — Pricing

- price books;
- discounts;
- promotions.

### Phase 7 — POS core

- register;
- shift;
- cart;
- sale;
- receipt;
- cash.

### Phase 8 — Payments

- payment abstraction;
- split payment;
- idempotency;
- reconciliation.

### Phase 9 — Offline and sync

- local DB;
- outbox;
- sync protocol;
- conflict handling;
- recovery.

### Phase 10 — Procurement

- suppliers;
- purchase requests;
- purchase orders;
- receiving.

### Phase 11 — Customers and loyalty

- CRM;
- points;
- coupons;
- gift/store credit.

### Phase 12 — Employees and approvals

- employee assignments;
- approvals;
- thresholds.

### Phase 13 — Reporting

- dashboards;
- aggregates;
- exports.

### Phase 14 — Subscription

- plans;
- entitlements;
- limits;
- trial;
- billing state;
- upgrades/downgrades.

### Phase 15 — Super Admin

- platform administration;
- support controls;
- global system health.

### Phase 16 — Integrations

- fiscal;
- payment providers;
- accounting;
- external APIs.

### Phase 17 — Hardening

- security;
- load;
- chaos;
- backup;
- recovery;
- DR;
- performance optimization.

Do not wait until Phase 17 to think about security.

Security requirements apply from Phase 1 onward.

Phase 17 is additional hardening.

---

# PART 141 — DEFINITION OF DONE FOR EACH FEATURE

A feature is not done until relevant items below are satisfied:

- domain model exists;
- validation exists;
- authorization exists;
- tenant scope exists;
- migrations exist;
- indexes reviewed;
- API exists;
- UI exists where required;
- error handling exists;
- logging/tracing exists;
- tests exist;
- audit exists when required;
- idempotency exists when required;
- concurrency considered;
- offline behavior considered;
- localization considered;
- permission matrix updated;
- documentation updated;
- no secrets added;
- build passes;
- automated tests pass.

---

# PART 142 — STOP CONDITIONS

Do not continue blindly when any of these are detected:

- potential cross-tenant exposure;
- destructive migration with unclear recovery;
- risk of duplicate financial transaction;
- risk of permanent data loss;
- broken authorization model;
- incompatible schema assumptions;
- ambiguous monetary calculation;
- security-critical requirement lacking enough information.

In these cases:

1. preserve repository integrity;
2. document the exact problem;
3. choose the safest reversible implementation if possible;
4. do not invent business/legal requirements.

---

# PART 143 — SAFE ASSUMPTION POLICY

When noncritical implementation details are unspecified:

choose the most conventional, maintainable, reversible professional default.

When the missing detail affects:

- money;
- legal compliance;
- tax;
- fiscalization;
- irreversible data;
- security ownership;
- customer data;

do not invent jurisdiction-specific rules.

Build an extensible abstraction and clearly document the unresolved configuration.

---

# PART 144 — VERSION CONTROL

Work in disciplined logical changes.

Do not produce one enormous unreviewable change if it can be safely separated.

Each meaningful implementation unit should:

- build;
- have tests;
- not knowingly leave repository broken.

Use descriptive commit history when commit capability is available and authorized.

Never commit secrets.

---

# PART 145 — NO PLACEHOLDER PRODUCTION BEHAVIOR

Do not mark unfinished logic as complete.

Avoid dangerous placeholders such as:

allowAllUsers = true

or:

TODO security later

inside production paths.

If a feature is unfinished, expose that clearly.

---

# PART 146 — NO FAKE INTEGRATIONS

Do not pretend payment/fiscal/email services are real when only mocks exist.

Use interfaces and development adapters.

Clearly separate:

- fake/test provider;
- sandbox provider;
- production provider.

---

# PART 147 — NO FAKE SUCCESS

If backend persistence fails, do not return success.

If synchronization fails, do not mark as synchronized.

If external payment is unknown, do not mark payment failed or successful without correct evidence.

State machines must support uncertain/reconciliation states when necessary.

---

# PART 148 — ERROR RECOVERY

Critical workflows must consider:

- process crash;
- machine restart;
- network disconnect;
- timeout after server completed request;
- duplicate retry;
- database failover;
- queue redelivery;
- client update during pending sync.

Test these scenarios.

---

# PART 149 — PERFORMANCE DISCIPLINE

For every critical feature ask:

- query count;
- index use;
- payload size;
- memory usage;
- sync cost;
- network round trips;
- cache behavior;
- concurrency behavior.

Do not optimize blindly.

Measure first.

---

# PART 150 — FINAL ARCHITECTURAL PRINCIPLES

SalekhPos must follow these principles:

**Secure by default.**

**Tenant isolated by default.**

**Offline capable for critical retail workflows.**

**Durable before acknowledged.**

**Idempotent where duplicate execution is dangerous.**

**Transactional where atomicity is required.**

**Auditable where business/security significance exists.**

**Configurable instead of country hardcoded.**

**Entitlement driven instead of plan-name hardcoded.**

**Modular instead of tangled.**

**Observable instead of opaque.**

**Recoverable instead of fragile.**

**Horizontally scalable instead of tied to one machine.**

**Gracefully degrading instead of all-or-nothing.**

**Simple enough for small shops and powerful enough for enterprise chains.**

---

# PART 151 — EXPECTED END STATE

A small store must be able to:

- register;
- configure business;
- add products;
- add employees;
- connect a terminal;
- receive stock;
- sell offline/online as allowed;
- take payments;
- print receipts;
- manage cash;
- see reports;

without being overwhelmed.

A medium retailer must additionally manage:

- many branches;
- warehouses;
- procurement;
- central catalog;
- regional managers;
- advanced roles;
- pricing;
- promotions;
- loyalty;
- employees;
- reporting.

A large enterprise must be able to grow toward:

- many regions;
- thousands of terminals;
- advanced identity;
- advanced audit;
- data residency;
- custom integrations;
- high availability;
- dedicated infrastructure;
- advanced disaster recovery;
- enterprise support.

The same SalekhPos platform must support that progression.

---

# PART 152 — FINAL CODEX DIRECTIVE

Read this entire specification before making architectural decisions.

Do not cherry-pick only the easiest requirements.

Do not implement visually impressive screens while ignoring backend correctness.

Do not implement backend features without authorization.

Do not implement authorization without tenant isolation.

Do not implement sales without durability.

Do not implement payments without idempotency/reconciliation.

Do not implement inventory without traceable movement semantics.

Do not implement offline mode without reliable synchronization.

Do not implement backups without recovery testing.

Do not implement global support by merely translating labels.

Do not implement enterprise scaling by merely adding the word "scalable" to documentation.

Every important claim must be demonstrated through architecture, configuration, tests, metrics or documentation.

Proceed systematically.

First inspect the repository and produce the Current State Assessment, Target Architecture, Gap Analysis and Implementation Roadmap.

Then implement SalekhPos phase by phase.

At the end of each phase:

1. build the complete relevant solution;
2. run all existing and new automated tests;
3. run static analysis/linting;
4. inspect migrations;
5. inspect authorization and tenant isolation;
6. inspect logs for sensitive information;
7. verify no placeholder security logic exists;
8. update architecture/documentation;
9. record remaining risks;
10. only then continue.

Never knowingly leave the repository in a broken state.

The objective is not to generate the most code.

The objective is to build the **most reliable, professional and evolvable SalekhPos implementation possible from the available requirements and infrastructure**.