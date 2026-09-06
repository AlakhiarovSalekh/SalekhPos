# SALEKHPOS
# COMPLETE MASTER ENGINEERING SPECIFICATION
# ARCHITECTURE, FILE STRUCTURE, SECURITY, RELIABILITY, GLOBALIZATION, SUBSCRIPTIONS, OFFLINE SYNC, TESTING AND IMPLEMENTATION CONTRACT

You are responsible for designing and implementing **SalekhPos**, a real, production-grade, global retail operating platform.

This is NOT:

- a demo;
- a toy project;
- a university assignment;
- a prototype;
- a temporary MVP;
- a simple cash-register application;
- a UI mockup;
- a fake enterprise application.

This system must be engineered as the foundation of a serious commercial SaaS platform capable of serving:

- a one-register small shop;
- a medium-size retail business;
- a multi-branch supermarket;
- a large retail chain;
- potentially very large international retail organizations operating in multiple countries.

The system must ultimately support:

- Web;
- Windows;
- macOS;
- Linux;
- iOS;
- Android.

The system must be designed for:

- multiple tenants;
- multiple organizations;
- multiple businesses/brands;
- multiple regions;
- multiple branches;
- multiple warehouses;
- multiple terminals;
- multiple devices;
- multiple languages;
- multiple countries;
- multiple currencies;
- multiple tax/fiscal configurations;
- multiple subscription tiers;
- offline operation;
- high availability;
- horizontal scaling;
- fault tolerance;
- disaster recovery;
- professional security;
- professional observability;
- long-term maintainability.

This document is the authoritative engineering contract.

Do not implement only the easy or visually impressive parts.

Do not interpret ambiguous areas casually.

Do not knowingly introduce shortcuts that compromise:

- security;
- tenant isolation;
- financial correctness;
- inventory integrity;
- data durability;
- recoverability;
- scalability;
- maintainability;
- auditability.

---

# 1. FUNDAMENTAL ENGINEERING PHILOSOPHY

Every important feature must be evaluated for:

- correctness;
- authentication;
- authorization;
- tenant isolation;
- business scope;
- validation;
- input integrity;
- output integrity;
- transactions;
- concurrency;
- idempotency;
- retries;
- duplicate requests;
- partial failure;
- process crash;
- device crash;
- network failure;
- offline operation;
- synchronization;
- conflict handling;
- data loss;
- data corruption;
- auditing;
- observability;
- localization;
- timezone;
- currency;
- tax;
- accessibility;
- performance;
- scalability;
- compatibility;
- migrations;
- security testing;
- automated testing;
- recovery.

A feature is not complete because its happy path works.

---

# 2. ABSOLUTE RULES

## 2.1 Never trust clients

All clients are untrusted:

- Web;
- Desktop;
- Mobile;
- POS;
- third-party integrations.

Never trust client-provided:

- tenant ID;
- branch ID;
- business ID;
- role;
- permission;
- price;
- discount;
- tax;
- inventory;
- payment state;
- approval state;
- subscription state;
- feature entitlement.

Recalculate and authorize critical values server-side.

---

## 2.2 Never silently lose critical data

Do not acknowledge success before durable persistence when durable persistence is required.

Never say:

"Sale completed"

if the sale only exists in memory.

Never say:

"Payment succeeded"

if payment state is unknown.

Critical business operations must either:

- complete safely;
- remain in a recoverable pending state;
- or clearly fail.

Never silently discard:

- sales;
- payment events;
- refunds;
- stock movements;
- goods receipts;
- transfers;
- cash movements;
- subscription state changes;
- security events;
- approval operations.

---

## 2.3 Never use float/double for money

Money must use deterministic fixed-precision decimal values.

Every monetary amount must have explicit currency context.

Never allow historical financial values to change because a current exchange rate changed.

---

## 2.4 Never hardcode one country

Never hardcode:

- GEL;
- USD;
- EUR;
- 18% VAT;
- Georgia;
- US sales tax;
- one timezone;
- one language;
- one measurement system;
- one receipt format.

Country-specific logic must live behind configurable country/fiscal adapters.

---

## 2.5 Never use UI visibility as authorization

A hidden button is not security.

Every protected operation must be checked on the server.

---

## 2.6 Never perform dangerous unbounded queries

Do not:

- SELECT entire huge tables;
- return millions of records;
- perform N+1 queries;
- run full scans on POS hot paths;
- load massive datasets into RAM unnecessarily.

Use:

- indexes;
- pagination;
- cursor pagination;
- projections;
- aggregation;
- query limits.

---

## 2.7 Never create unnecessary single points of failure

Production architecture must support redundancy for critical components.

---

## 2.8 Never rely on cache for durable business truth

Cache failure must not permanently destroy business data.

---

## 2.9 Never use unlimited retries

Retries must be:

- bounded;
- observable;
- exponential;
- jittered where useful;
- safe.

---

## 2.10 Never perform irreversible destructive operations casually

Critical deletion must be:

- authorized;
- audited;
- recoverable when appropriate;
- legally compliant.

---

# 3. PRODUCT DEFINITION

SalekhPos is:

> A global, multi-tenant, cross-platform, offline-capable, multi-country, multi-language, multi-currency retail operating platform.

SalekhPos is NOT merely POS.

The complete platform includes:

- POS;
- Product Catalog;
- Pricing;
- Promotions;
- Inventory;
- Warehousing;
- Procurement;
- Suppliers;
- Customers;
- CRM;
- Loyalty;
- Employees;
- Shifts;
- Cash Management;
- Payments;
- Returns;
- Finance;
- Reporting;
- Analytics;
- Notifications;
- Security;
- Audit;
- Global Localization;
- Country Configuration;
- Subscription;
- SaaS Administration;
- Offline Sync;
- Device Management;
- Integrations;
- Platform Administration.

---

# 4. SUPPORTED PLATFORMS

## Web

Modern browsers.

Used heavily for:

- owner dashboards;
- managers;
- inventory;
- procurement;
- reports;
- accounting views;
- administration;
- Super Admin.

## Desktop

Must support:

- Windows;
- macOS;
- Linux.

Desktop POS is especially important for:

- receipt printers;
- scanners;
- cash drawers;
- scales;
- fiscal devices;
- customer displays;
- offline use.

## Mobile

Must support:

- iOS;
- Android.

Mobile may support:

- owner dashboard;
- manager functions;
- approvals;
- barcode scanning;
- stock counts;
- warehouse receiving;
- inventory transfers;
- notifications;
- reports;
- mobile POS where appropriate.

---

# 5. BUSINESS SIZE MODEL

SalekhPos must support three broad business categories.

## SMALL BUSINESS

Examples:

- one small shop;
- kiosk;
- mini-market;
- café;
- one or a few terminals;
- few employees.

## MEDIUM BUSINESS

Examples:

- several branches;
- several warehouses;
- dozens/hundreds of employees;
- centralized procurement;
- centralized inventory;
- regional management.

## LARGE / ENTERPRISE

Examples:

- many branches;
- many regions;
- many warehouses;
- thousands of employees;
- high transaction volume;
- enterprise integrations;
- SSO;
- data residency;
- advanced DR;
- custom SLA.

Do NOT build separate products for each.

Use one platform with:

- modular features;
- entitlements;
- permissions;
- configuration;
- simpler UI for smaller customers.

---

# 6. SUBSCRIPTION MODEL

Subscription must be entitlement-based.

Do NOT implement logic like:

if plan == "Premium"

Instead implement:

HasFeature("AdvancedReporting")

and resource limits.

Suggested marketing structure:

## SMALL

- Start
- Grow
- Plus

## MEDIUM

- Standard
- Advanced
- Premium

## LARGE / ENTERPRISE

- Enterprise
- Enterprise Plus
- Enterprise Custom

These names are marketing labels, not business logic.

---

# 7. SUBSCRIPTION ENTITLEMENTS

Possible feature entitlements:

- POS;
- inventory;
- multi-branch;
- warehouse;
- procurement;
- supplier management;
- loyalty;
- employee management;
- advanced pricing;
- promotions;
- advanced reports;
- API access;
- accounting integrations;
- custom roles;
- SSO;
- audit export;
- advanced security;
- data residency;
- advanced BI;
- enterprise support.

---

# 8. RESOURCE LIMITS

Plans may define:

- branch count;
- active terminal count;
- warehouse count;
- employee count;
- API limits;
- storage;
- optional communication quotas.

---

# 9. ADD-ONS

Support optional add-ons:

- extra terminal;
- extra branch;
- extra warehouse;
- employee pack;
- advanced analytics;
- accounting integration;
- API;
- extra storage;
- premium support;
- messaging pack.

---

# 10. SUBSCRIPTION STATE MACHINE

Support:

Trial
→ Active
→ Grace Period
→ Past Due
→ Suspended
→ Cancelled
→ Expired

Do not instantly disable a store's POS because one payment attempt failed.

Use safe grace periods.

---

# 11. DOWNGRADE SAFETY

Never delete customer data due to downgrade.

Example:

Current warehouses = 15.

New plan allows 5.

Do NOT delete 10.

Instead:

- preserve all existing data;
- restrict creating additional resources;
- clearly notify owner;
- preserve exports/access according to policy.

---

# 12. REGIONAL SUBSCRIPTION PRICING

Support:

- monthly;
- yearly;
- regional price books;
- local billing currencies;
- regional taxes;
- discounts;
- coupons;
- trials;
- custom enterprise contracts.

Do not simply convert one USD price using realtime FX and assume that is always the commercial price.

---

# 13. BUSINESS HIERARCHY

Core hierarchy:

Platform
→ Tenant / Organization
→ Business / Brand
→ Region
→ Branch
→ Warehouse / Stock Location
→ Terminal / Device
→ Employee/User

Do not assume:

- one tenant = one business;
- one business = one branch;
- one user = one branch;
- one warehouse = one branch.

---

# 14. PLATFORM OWNER

There must be:

Platform Owner

and:

Super Admin.

Platform Owner is the highest authority.

Only Platform Owner can create another Super Admin by default.

A normal Super Admin cannot:

- remove Platform Owner;
- demote Platform Owner;
- create another Super Admin unless explicitly allowed;
- make themselves Platform Owner;
- bypass auditing.

---

# 15. PLATFORM OWNER SECURITY

Require strong protection:

- MFA;
- passkeys where possible;
- security keys where possible;
- session listing;
- device listing;
- reauthentication;
- high-risk action confirmation;
- recovery protections;
- security alerts;
- full audit.

---

# 16. DEFAULT BUSINESS ROLES

Provide professional defaults:

- Business Owner;
- Regional Manager;
- Branch Manager;
- Assistant Manager;
- Inventory Manager;
- Warehouse Manager;
- Warehouse Clerk;
- Receiving Clerk;
- Procurement Manager;
- Purchasing Officer;
- Cash Supervisor;
- Senior Cashier;
- Cashier;
- Finance Manager;
- Accountant;
- HR Manager;
- Customer Service;
- Loss Prevention;
- Auditor;
- IT Administrator.

These are defaults only.

Custom roles must be supported.

---

# 17. PERMISSION ENGINE

Examples:

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

---

# 18. PERMISSION SCOPES

Permissions must support scopes:

- own;
- terminal;
- warehouse;
- branch;
- multiple branches;
- region;
- business;
- organization;
- platform.

Permission alone is insufficient.

Example:

inventory.view

for Branch A

must not grant access to Branch B.

---

# 19. GLOBALIZATION

Internationalization is core architecture.

Support:

- multilingual UI;
- user-specific language;
- business language;
- branch locale;
- RTL;
- Unicode;
- currency formatting;
- number formatting;
- date formatting;
- time formatting;
- phone formatting;
- address formatting;
- measurement units;
- locale-specific behavior.

---

# 20. RTL SUPPORT

UI architecture must support:

- left-to-right;
- right-to-left.

Do not hardcode layout assumptions.

---

# 21. TIMEZONES

Canonical server timestamps should generally be UTC.

Every branch must have explicit timezone.

Support daylight saving correctly.

"Today's sales" must reflect the reporting timezone.

---

# 22. CURRENCIES

Every monetary transaction must know:

- currency;
- amount;
- FX rate if converted;
- historical applied FX information.

Base currency may exist per business.

---

# 23. COUNTRY CONFIGURATION

Create configurable structures for:

- country;
- currencies;
- languages;
- timezone;
- tax;
- fiscal requirements;
- receipt rules;
- phone;
- address;
- rounding;
- units;
- privacy;
- regulatory integrations.

---

# 24. COUNTRY ADAPTERS

Do not spread country logic throughout core code.

Use interfaces/adapters like:

ICountryFiscalAdapter
ITaxAdapter
IReceiptFiscalAdapter

Examples may later include:

GeorgiaFiscalAdapter
GermanyFiscalAdapter
USRegionTaxAdapter

but do NOT fake legal rules.

---

# 25. PRODUCT CATALOG

Product should support:

- ID;
- SKU;
- name;
- localized names;
- description;
- category;
- brand;
- manufacturer;
- suppliers;
- tax category;
- unit;
- weight;
- dimensions;
- barcodes;
- variants;
- images;
- serial tracking;
- batch tracking;
- expiry tracking;
- status.

---

# 26. PRODUCT VARIANTS

Support variant dimensions like:

- size;
- color;
- material;
- package.

Each variant may have:

- SKU;
- barcode;
- price;
- stock;
- cost;
- attributes.

---

# 27. BARCODE ENGINE

Support common types:

- EAN;
- UPC;
- Code 128;
- QR;
- internal barcodes;
- weighted barcodes;
- price-embedded barcodes.

---

# 28. INVENTORY LEDGER

Never model inventory only as:

stock = 50

Use StockMovement/InventoryLedger.

Examples:

+100 purchase
-2 sale
+1 return
-3 damaged
-10 transfer out
+10 transfer in

Movement must include:

- tenant;
- business;
- branch;
- location;
- product;
- variant;
- quantity;
- reason;
- source;
- time;
- actor;
- approval;
- correlation ID.

---

# 29. INVENTORY STATES

Define meanings for:

- on hand;
- available;
- reserved;
- in transit;
- damaged;
- quarantine.

Do not use ambiguous one-number stock everywhere.

---

# 30. WAREHOUSE LOCATIONS

Optional hierarchy:

Warehouse
→ Zone
→ Aisle
→ Rack
→ Shelf
→ Bin

Small stores must not be forced to use all levels.

---

# 31. BATCH / LOT / EXPIRY

Support optional:

- lot;
- batch;
- manufacturing date;
- expiry;
- supplier;
- cost;
- received date.

---

# 32. SERIAL TRACKING

Support:

- serial number;
- IMEI where appropriate;
- individual unit traceability.

---

# 33. INVENTORY COUNT

Support:

- full count;
- cycle count;
- blind count;
- mobile scanning;
- variance;
- approvals;
- adjustment movement.

---

# 34. PROCUREMENT

Flow:

Reorder Suggestion
→ Purchase Request
→ Approval
→ Purchase Order
→ Supplier
→ Delivery
→ Goods Receipt
→ Inspection
→ Inventory
→ Supplier Invoice
→ Supplier Return if needed

Support:

- partial delivery;
- cancellation;
- backorders;
- over/under delivery.

---

# 35. SUPPLIERS

Supplier profiles may include:

- company;
- contacts;
- addresses;
- currencies;
- products;
- terms;
- lead time;
- history;
- outstanding balance;
- performance;
- returns.

---

# 36. AUTOMATIC REORDER

Use:

- current stock;
- minimum;
- safety stock;
- sales velocity;
- lead time;
- open POs;
- demand.

Automatic suggestion does not mean automatic purchasing.

---

# 37. POS CORE

POS must be extremely responsive.

Flow:

Login/PIN
→ Open Shift
→ Scan/Search
→ Cart
→ Customer optional
→ Discounts/Promotions
→ Payment
→ Durable Sale
→ Receipt
→ Next Customer

---

# 38. OFFLINE POS

When internet is unavailable and local legal/payment rules allow:

- barcode lookup works;
- cart works;
- cash sale works;
- local receipt works;
- sale persists locally;
- sync queue persists;
- POS remains usable.

Never fake online payment approval offline.

---

# 39. LOCAL DATABASE

Use reliable embedded database such as SQLite.

May contain:

- product cache;
- price cache;
- barcode index;
- tax configuration;
- promotion rules needed for POS;
- cart recovery;
- local sales;
- sync outbox;
- local metadata.

---

# 40. SYNC ENGINE

Every sync operation should have:

- OperationId;
- TenantId;
- BusinessId;
- BranchId;
- TerminalId;
- DeviceId;
- Entity/Event type;
- Payload;
- Local sequence;
- Timestamp;
- Version;
- Status;
- RetryCount;
- LastError.

Sync must support:

- duplicates;
- retry;
- resume;
- offline periods;
- chunking;
- conflict handling;
- monitoring.

---

# 41. INITIAL SYNC

Large catalog sync must be:

- chunked;
- resumable;
- memory efficient;
- progress-aware.

Do not freeze UI.

---

# 42. CONFLICT RESOLUTION

No universal Last Write Wins.

Use domain-specific rules.

Inventory:
ledger/events.

Settings:
optimistic concurrency.

Financial data:
never casually overwrite.

---

# 43. OUTBOX / INBOX

Use transactional outbox for:

database change
+
event publication.

Use inbox/deduplication for repeated event consumption.

---

# 44. CART RECOVERY

Persist cart state safely.

App crash must not unnecessarily destroy current cart.

Cart recovery must not automatically create sale.

---

# 45. SALES MODEL

Sale should store historical snapshot data.

Include:

- sale ID;
- tenant;
- business;
- branch;
- terminal;
- cashier;
- customer;
- currency;
- items;
- quantities;
- unit prices;
- discounts;
- tax;
- promotion;
- payment state;
- status;
- timestamps;
- receipt references.

Changing product later must not change historical sale.

---

# 46. PRICING ENGINE

Support:

- base price;
- branch price;
- regional price;
- customer price;
- wholesale;
- scheduled;
- promotional price.

Server calculates final price.

---

# 47. PROMOTIONS

Support architecture for:

- percentage;
- fixed amount;
- buy X get Y;
- quantity pricing;
- bundles;
- scheduled promotion;
- coupon;
- category;
- loyalty;
- customer group.

Promotion stacking/conflict must be deterministic.

---

# 48. MANUAL DISCOUNTS

Require:

- permission;
- thresholds;
- optional approval;
- reason;
- audit.

---

# 49. PAYMENTS

Core payment types:

- cash;
- card;
- bank;
- gift card;
- store credit;
- voucher;
- split payment.

Payment providers must be adapters.

---

# 50. PAYMENT IDEMPOTENCY

Critical.

Duplicate request must not double charge.

Persist:

- idempotency key;
- provider reference;
- payment state;
- reconciliation state.

---

# 51. SPLIT PAYMENT

Example:

100 total:

20 cash
50 card
30 gift card

Validate totals.

---

# 52. CASH MANAGEMENT

Support:

- opening balance;
- cash sale;
- cash refund;
- cash in;
- cash out;
- safe drop;
- expected;
- counted;
- difference;
- close.

---

# 53. SHIFTS

Support:

- start;
- active;
- handover;
- close;
- forced close;
- discrepancy review.

---

# 54. RETURNS

Support:

- full return;
- partial return;
- receipt return;
- no-receipt controlled return;
- reason;
- approval;
- stock disposition;
- refund method.

Stock disposition:

- available;
- damaged;
- quarantine;
- supplier return.

---

# 55. RECEIPTS

Support:

- print;
- email;
- digital;
- QR;
- PDF;
- fiscal.

---

# 56. HARDWARE ABSTRACTION

Create interfaces such as:

IBarcodeScanner
IReceiptPrinter
ICashDrawer
IWeighingScale
ICustomerDisplay
ILabelPrinter
ICardTerminal
IFiscalDevice

Do not place vendor code directly in business logic.

---

# 57. CUSTOMERS / CRM

Support:

- profile;
- contacts;
- addresses;
- language;
- purchase history;
- loyalty;
- credit;
- gift cards;
- returns;
- consent;
- privacy settings.

---

# 58. LOYALTY

Support:

- points;
- tiers;
- rewards;
- coupons;
- membership;
- store credit;
- gift cards.

Prevent duplicate redemption.

---

# 59. EMPLOYEES

Support:

- profile;
- user;
- roles;
- branches;
- warehouses;
- status;
- shifts;
- attendance;
- performance;
- permissions.

---

# 60. APPROVAL ENGINE

Generic approval system.

Support:

- requester;
- approver;
- thresholds;
- multi-step;
- expiration;
- approval;
- rejection;
- reasons;
- audit.

---

# 61. AUDIT

Audit is mandatory for critical operations.

Track:

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
- relevant network metadata.

Audit logs must not be editable by normal users.

---

# 62. LOSS PREVENTION

Flag anomalies:

- excessive refunds;
- many voids;
- large discounts;
- inventory adjustments;
- cash discrepancy;
- suspicious exports;
- suspicious login;
- unusual privilege use.

Do not automatically accuse users.

---

# 63. REPORTING

Do not overload OLTP database.

Use:

- indexes;
- aggregates;
- materialized summaries;
- background generation;
- read replicas later;
- analytical database later if required.

---

# 64. OWNER DASHBOARD

Potential metrics:

- revenue;
- gross profit;
- sales;
- refunds;
- discounts;
- inventory value;
- cash;
- top products;
- weak products;
- branch comparison;
- employee performance;
- stockout;
- shrinkage.

---

# 65. SUPER ADMIN DASHBOARD

Platform Owner/Super Admin sees:

- tenants;
- businesses;
- branches;
- users;
- plans;
- subscriptions;
- terminals;
- active devices;
- errors;
- API latency;
- database health;
- jobs;
- queues;
- sync;
- security alerts;
- support;
- country distribution;
- feature flags.

---

# 66. SUPPORT MODE

Support access must be explicit and audited.

Record:

- support employee;
- tenant;
- reason;
- start;
- end;
- actions.

---

# 67. BACKEND ARCHITECTURE

Use **Modular Monolith initially**.

Do not prematurely use dozens of microservices.

Modules must have clear boundaries and be extractable later.

---

# 68. BACKEND TECHNOLOGY

Use:

ASP.NET Core
.NET LTS

Primary database:

PostgreSQL.

Local database:

SQLite.

Cache:

Redis-compatible layer where needed.

Realtime:

SignalR/WebSocket.

Web:

React + TypeScript.

Cross-platform client:

Flutter is acceptable for:

- Android;
- iOS;
- Windows;
- macOS;
- Linux;

provided native hardware integration is implemented properly.

---

# 69. COMPLETE REPOSITORY STRUCTURE

Use a clean monorepo structure.

Target structure:

```text
SalekhPos/
│
├── apps/
│   │
│   ├── web/
│   │   ├── public/
│   │   ├── src/
│   │   │   ├── app/
│   │   │   │   ├── providers/
│   │   │   │   ├── bootstrap/
│   │   │   │   └── config/
│   │   │   │
│   │   │   ├── routing/
│   │   │   ├── layouts/
│   │   │   ├── localization/
│   │   │   ├── state/
│   │   │   ├── services/
│   │   │   ├── hooks/
│   │   │   ├── utilities/
│   │   │   │
│   │   │   ├── shared/
│   │   │   │   ├── components/
│   │   │   │   ├── forms/
│   │   │   │   ├── tables/
│   │   │   │   ├── dialogs/
│   │   │   │   ├── validation/
│   │   │   │   └── accessibility/
│   │   │   │
│   │   │   └── features/
│   │   │       ├── auth/
│   │   │       ├── dashboard/
│   │   │       ├── businesses/
│   │   │       ├── branches/
│   │   │       ├── products/
│   │   │       ├── pricing/
│   │   │       ├── promotions/
│   │   │       ├── inventory/
│   │   │       ├── warehouses/
│   │   │       ├── procurement/
│   │   │       ├── suppliers/
│   │   │       ├── sales/
│   │   │       ├── returns/
│   │   │       ├── customers/
│   │   │       ├── loyalty/
│   │   │       ├── employees/
│   │   │       ├── approvals/
│   │   │       ├── reports/
│   │   │       ├── finance/
│   │   │       ├── subscriptions/
│   │   │       ├── settings/
│   │   │       └── platform-admin/
│   │   │
│   │   ├── tests/
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   └── build-config/
│   │
│   └── client/
│       ├── lib/
│       │   ├── app/
│       │   ├── core/
│       │   │   ├── networking/
│       │   │   ├── authentication/
│       │   │   ├── authorization/
│       │   │   ├── localization/
│       │   │   ├── configuration/
│       │   │   ├── persistence/
│       │   │   ├── telemetry/
│       │   │   ├── error_handling/
│       │   │   └── utilities/
│       │   │
│       │   ├── shared/
│       │   │   ├── widgets/
│       │   │   ├── models/
│       │   │   ├── validation/
│       │   │   └── design_system/
│       │   │
│       │   ├── features/
│       │   │   ├── auth/
│       │   │   ├── pos/
│       │   │   ├── shifts/
│       │   │   ├── products/
│       │   │   ├── pricing/
│       │   │   ├── inventory/
│       │   │   ├── warehouses/
│       │   │   ├── receiving/
│       │   │   ├── transfers/
│       │   │   ├── customers/
│       │   │   ├── employees/
│       │   │   ├── approvals/
│       │   │   ├── reports/
│       │   │   └── settings/
│       │   │
│       │   ├── sync/
│       │   │   ├── engine/
│       │   │   ├── outbox/
│       │   │   ├── inbox/
│       │   │   ├── conflict/
│       │   │   └── checkpoints/
│       │   │
│       │   ├── hardware/
│       │   │   ├── scanner/
│       │   │   ├── printer/
│       │   │   ├── cash_drawer/
│       │   │   ├── scale/
│       │   │   ├── display/
│       │   │   ├── label_printer/
│       │   │   ├── payment_terminal/
│       │   │   └── fiscal_device/
│       │   │
│       │   └── platform/
│       │       ├── windows/
│       │       ├── macos/
│       │       ├── linux/
│       │       ├── android/
│       │       └── ios/
│       │
│       ├── android/
│       ├── ios/
│       ├── windows/
│       ├── macos/
│       ├── linux/
│       ├── test/
│       └── integration_test/
│
├── backend/
│   ├── SalekhPos.sln
│   │
│   ├── src/
│   │   │
│   │   ├── SalekhPos.Api/
│   │   │   ├── Configuration/
│   │   │   ├── Middleware/
│   │   │   ├── Authentication/
│   │   │   ├── Authorization/
│   │   │   ├── Health/
│   │   │   ├── OpenApi/
│   │   │   ├── Versioning/
│   │   │   ├── Observability/
│   │   │   └── Program.cs
│   │   │
│   │   ├── SalekhPos.Domain/
│   │   │   ├── Common/
│   │   │   ├── Primitives/
│   │   │   ├── ValueObjects/
│   │   │   ├── DomainEvents/
│   │   │   ├── Exceptions/
│   │   │   └── Abstractions/
│   │   │
│   │   ├── SalekhPos.Application/
│   │   │   ├── Behaviors/
│   │   │   ├── Validation/
│   │   │   ├── Authorization/
│   │   │   ├── Transactions/
│   │   │   ├── Idempotency/
│   │   │   ├── Messaging/
│   │   │   └── Abstractions/
│   │   │
│   │   ├── SalekhPos.Infrastructure/
│   │   │   ├── Persistence/
│   │   │   ├── Caching/
│   │   │   ├── Messaging/
│   │   │   ├── Storage/
│   │   │   ├── Security/
│   │   │   ├── Observability/
│   │   │   ├── Integrations/
│   │   │   └── Resilience/
│   │   │
│   │   ├── SalekhPos.Contracts/
│   │   │   ├── Requests/
│   │   │   ├── Responses/
│   │   │   ├── Events/
│   │   │   └── Common/
│   │   │
│   │   ├── SalekhPos.Workers/
│   │   │   ├── Notifications/
│   │   │   ├── Reports/
│   │   │   ├── Imports/
│   │   │   ├── Exports/
│   │   │   ├── Sync/
│   │   │   ├── Reconciliation/
│   │   │   └── Integrations/
│   │   │
│   │   └── Modules/
│   │       │
│   │       ├── Identity/
│   │       ├── Tenancy/
│   │       ├── Organizations/
│   │       ├── Businesses/
│   │       ├── Branches/
│   │       ├── Regions/
│   │       ├── Devices/
│   │       ├── Employees/
│   │       ├── Authorization/
│   │       ├── Catalog/
│   │       ├── Pricing/
│   │       ├── Promotions/
│   │       ├── Inventory/
│   │       ├── Warehousing/
│   │       ├── Procurement/
│   │       ├── Suppliers/
│   │       ├── Sales/
│   │       ├── Payments/
│   │       ├── Returns/
│   │       ├── Customers/
│   │       ├── Loyalty/
│   │       ├── Finance/
│   │       ├── Reporting/
│   │       ├── Notifications/
│   │       ├── Audit/
│   │       ├── Sync/
│   │       ├── Localization/
│   │       ├── CountryConfiguration/
│   │       ├── Subscriptions/
│   │       ├── Integrations/
│   │       └── PlatformAdministration/
│   │
│   └── tests/
│       ├── SalekhPos.UnitTests/
│       ├── SalekhPos.IntegrationTests/
│       ├── SalekhPos.ArchitectureTests/
│       ├── SalekhPos.SecurityTests/
│       ├── SalekhPos.TenantIsolationTests/
│       ├── SalekhPos.ContractTests/
│       └── SalekhPos.PerformanceTests/
│
├── database/
│   ├── migrations/
│   ├── seeds/
│   ├── scripts/
│   ├── maintenance/
│   ├── partitioning/
│   ├── backup/
│   ├── restore/
│   └── docs/
│
├── packages/
│   ├── contracts/
│   ├── localization/
│   ├── design-system/
│   ├── validation/
│   ├── api-client/
│   ├── telemetry/
│   └── shared-types/
│
├── integrations/
│   ├── payments/
│   ├── fiscal/
│   ├── accounting/
│   ├── ecommerce/
│   ├── shipping/
│   ├── messaging/
│   ├── tax/
│   └── identity/
│
├── infrastructure/
│   ├── docker/
│   ├── compose/
│   ├── environments/
│   │   ├── development/
│   │   ├── test/
│   │   ├── staging/
│   │   └── production/
│   │
│   ├── terraform/
│   ├── cloud/
│   ├── networking/
│   ├── load-balancing/
│   ├── database/
│   ├── redis/
│   ├── messaging/
│   ├── storage/
│   ├── monitoring/
│   ├── logging/
│   ├── tracing/
│   ├── alerting/
│   ├── backup/
│   ├── disaster-recovery/
│   ├── security/
│   └── kubernetes/
│
├── docs/
│   ├── architecture/
│   │   ├── overview/
│   │   ├── domain-model/
│   │   ├── module-boundaries/
│   │   ├── data-flow/
│   │   ├── deployment/
│   │   └── diagrams/
│   │
│   ├── adr/
│   ├── api/
│   ├── security/
│   │   ├── authentication/
│   │   ├── authorization/
│   │   ├── tenant-isolation/
│   │   ├── threat-model/
│   │   └── incident-response/
│   │
│   ├── database/
│   ├── inventory/
│   ├── pos/
│   ├── payments/
│   ├── offline-sync/
│   ├── permissions/
│   ├── subscriptions/
│   ├── localization/
│   ├── country-config/
│   ├── integrations/
│   ├── hardware/
│   ├── deployment/
│   ├── monitoring/
│   ├── backup-recovery/
│   ├── disaster-recovery/
│   └── runbooks/
│
├── scripts/
│   ├── development/
│   ├── database/
│   ├── migration/
│   ├── backup/
│   ├── restore/
│   ├── ci/
│   ├── release/
│   ├── security/
│   └── maintenance/
│
├── tests/
│   ├── e2e/
│   ├── load/
│   ├── stress/
│   ├── chaos/
│   ├── recovery/
│   ├── offline/
│   ├── synchronization/
│   └── hardware/
│
├── tools/
│   ├── generators/
│   ├── diagnostics/
│   ├── database/
│   ├── importers/
│   └── local-development/
│
├── .github/
│   ├── workflows/
│   ├── CODEOWNERS
│   ├── ISSUE_TEMPLATE/
│   └── PULL_REQUEST_TEMPLATE.md
│
├── .editorconfig
├── .gitattributes
├── .gitignore
├── Directory.Build.props
├── Directory.Packages.props
├── docker-compose.yml
├── README.md
├── SECURITY.md
├── CONTRIBUTING.md
└── LICENSE
```

---

# 70. MODULE INTERNAL STRUCTURE

Every major backend module should follow a consistent internal architecture.

Example:

```text
Inventory/
│
├── Domain/
│   ├── Entities/
│   ├── Aggregates/
│   ├── ValueObjects/
│   ├── DomainEvents/
│   ├── Rules/
│   ├── Policies/
│   ├── Services/
│   └── Exceptions/
│
├── Application/
│   ├── Commands/
│   ├── Queries/
│   ├── Handlers/
│   ├── DTOs/
│   ├── Validators/
│   ├── Mappers/
│   ├── Authorization/
│   └── Interfaces/
│
├── Infrastructure/
│   ├── Persistence/
│   │   ├── Configurations/
│   │   ├── Repositories/
│   │   └── Queries/
│   │
│   ├── Integrations/
│   ├── Caching/
│   └── Messaging/
│
├── Api/
│   ├── Endpoints/
│   ├── Contracts/
│   └── Authorization/
│
└── Tests/
    ├── Unit/
    ├── Integration/
    └── Security/
```

Do not force useless folders when module is tiny, but maintain conceptual separation.

---

# 71. FRONTEND FEATURE STRUCTURE

Do not use one huge:

screens/
services/
models/

folder.

Use feature-based structure.

Example:

```text
features/
└── inventory/
    ├── api/
    ├── components/
    ├── domain/
    ├── hooks/
    ├── state/
    ├── screens/
    ├── validation/
    ├── permissions/
    └── tests/
```

---

# 72. DATABASE

Use PostgreSQL.

Design tables carefully.

Evaluate:

- primary key;
- tenant ownership;
- business scope;
- branch scope;
- relationships;
- foreign keys;
- constraints;
- indexes;
- version/concurrency;
- soft delete;
- retention;
- timestamps.

---

# 73. DATABASE HIGH AVAILABILITY

Production architecture should support:

Primary PostgreSQL
+
Standby
+
Failover
+
Read replicas as needed
+
PITR
+
Backups

---

# 74. DATA LOSS PROTECTION

Use multiple layers:

- ACID transactions;
- WAL;
- replication;
- automated backups;
- point-in-time recovery;
- encrypted backups;
- cross-zone copies;
- cross-region copies;
- immutable backups;
- restore drills.

---

# 75. BACKUP SECURITY

Production compromise must not automatically permit deleting all backups.

Use separate credentials/security boundaries.

---

# 76. RESTORE TESTING

Regularly test:

- backup restore;
- DB startup;
- schema integrity;
- application startup;
- critical business smoke tests.

---

# 77. SOFT DELETE

Use where appropriate for critical business records.

Do NOT blindly soft-delete everything.

---

# 78. CACHE

Use Redis where justified.

Cache may hold:

- product lookup;
- reference configuration;
- permission metadata;
- sessions where appropriate;
- frequently accessed data.

Cache loss must not permanently lose data.

---

# 79. QUEUES

Use durable queue where appropriate for:

- notifications;
- reports;
- imports;
- exports;
- sync;
- integration events;
- reconciliation.

Workers must be horizontally scalable.

---

# 80. JOB FAILURE

Support:

- retry;
- backoff;
- dead-letter;
- alerting;
- manual replay where safe.

---

# 81. HORIZONTAL SCALING

Backend should be stateless where practical.

Example:

Load Balancer
→ API-1
→ API-2
→ API-3
→ API-N

---

# 82. GRACEFUL DEGRADATION

Examples:

Analytics fails:
POS continues.

Notifications fail:
sale completes, notification queues.

Realtime fails:
API continues.

Redis fails:
degraded performance, not total outage.

Accounting integration fails:
transaction persists, integration retries later.

---

# 83. API

Use:

- versioning;
- DTOs;
- validation;
- safe error model;
- pagination;
- authentication;
- authorization;
- idempotency.

Do not expose persistence models directly.

---

# 84. CONCURRENCY

Use appropriate mechanisms:

- optimistic concurrency;
- version fields;
- row locks where necessary;
- transaction isolation;
- domain constraints.

Example:

1 item remaining
+
2 simultaneous sales

must be handled according to configured negative-stock policy.

---

# 85. AUTHENTICATION

Support:

- password;
- strong hashing;
- MFA;
- passkeys;
- sessions;
- refresh rotation;
- revoke;
- device tracking;
- secure reset.

---

# 86. SECURITY PROTECTIONS

Apply:

- SQL injection prevention;
- XSS protection;
- CSRF protection;
- SSRF controls;
- secure CORS;
- secure headers;
- input validation;
- request-size limits;
- rate limiting;
- brute-force protection;
- secure cookies;
- output encoding;
- file validation;
- least privilege;
- encryption.

---

# 87. SECRETS

Never commit:

- passwords;
- API keys;
- production tokens;
- database credentials.

Use secrets manager.

---

# 88. OBJECT STORAGE

Use object storage for:

- images;
- documents;
- large reports;
- exports.

Protect by tenant.

---

# 89. GLOBAL INFRASTRUCTURE

Design for future:

Global DNS
→ CDN/Edge
→ Regional Application
→ Regional Data Services

Do not deploy unnecessary global complexity before needed.

---

# 90. DATA RESIDENCY

Architecture must support tenant data region.

Potential examples:

- EU;
- North America;
- APAC.

---

# 91. DEVICE MANAGEMENT

Track:

- device ID;
- terminal;
- tenant;
- branch;
- platform;
- app version;
- registration;
- last seen;
- sync state;
- revoked state.

---

# 92. TERMINAL PROVISIONING

Flow:

Install
→ Authenticate
→ Select permitted branch
→ Register device
→ Establish trust
→ Initial sync
→ Ready

Copying local DB must not clone trusted identity automatically.

---

# 93. PERFORMANCE

UI must remain responsive.

Do not perform:

- heavy work on UI thread;
- huge rendering;
- large parsing synchronously;
- blocking analytics on checkout.

---

# 94. PERFORMANCE SLOS

Measure:

- barcode lookup;
- cart update;
- checkout;
- API latency;
- search;
- dashboard;
- sync;
- queue backlog;
- database latency.

---

# 95. OBSERVABILITY

Implement:

- structured logs;
- metrics;
- traces;
- correlation IDs;
- health checks;
- alerts.

---

# 96. MONITORING METRICS

Monitor:

- request rate;
- latency;
- errors;
- CPU;
- RAM;
- DB latency;
- DB connections;
- replica lag;
- cache hit ratio;
- queue depth;
- job failure;
- sync backlog;
- payment error;
- login failure;
- terminal status.

---

# 97. HEALTH CHECKS

Separate where appropriate:

- liveness;
- readiness.

Monitor:

- API;
- DB;
- cache;
- queue;
- workers;
- storage;
- important providers.

---

# 98. DEPLOYMENT

Support safe strategies:

- rolling;
- canary;
- blue/green.

Use:

- health verification;
- rollback;
- automated pipelines.

---

# 99. DATABASE MIGRATIONS

Use safe migration practices.

Avoid long locks.

Use expand-and-contract where necessary.

Test against production-like datasets.

---

# 100. FEATURE FLAGS

Feature flags may be scoped by:

- tenant;
- country;
- plan;
- percentage;
- internal user.

Feature flags are not authorization.

---

# 101. ENVIRONMENTS

Maintain:

- Development;
- Test;
- Staging;
- Production.

Never use production as testing environment.

---

# 102. TESTING

Required categories:

- Unit;
- Integration;
- Contract;
- Architecture;
- Database;
- Security;
- Tenant isolation;
- Permission;
- E2E;
- Offline;
- Sync;
- Hardware;
- Performance;
- Load;
- Stress;
- Chaos;
- Recovery.

---

# 103. SECURITY TESTS

Explicitly test:

- IDOR;
- BOLA;
- cross-tenant access;
- role escalation;
- forged branch IDs;
- forged tenant IDs;
- broken scope;
- malicious input;
- brute force;
- session revocation;
- upload attacks;
- duplicate financial actions.

---

# 104. LOAD TESTING

Use realistic workloads:

- authentication;
- scanning;
- sale;
- inventory;
- dashboard;
- sync;
- reporting.

Do not claim unlimited scalability.

Measure actual capacity.

---

# 105. CHAOS TESTING

Simulate safely:

- API crash;
- worker crash;
- DB failover;
- Redis outage;
- queue delay;
- provider timeout;
- latency;
- network loss;
- device offline;
- duplicated sync;
- restart.

---

# 106. DISASTER RECOVERY

Prepare documented recovery for:

- DB failure;
- region failure;
- bad deployment;
- accidental deletion;
- credential compromise;
- ransomware;
- backup restore.

Define RPO and RTO.

---

# 107. IMPORT

Large import:

Upload
→ Validate
→ Preview
→ Background Job
→ Progress
→ Result

Do not block one web request.

---

# 108. EXPORT

Large exports must be background jobs.

Use secure temporary download URLs.

---

# 109. NOTIFICATIONS

Support:

- in-app;
- push;
- email;
- SMS adapter;
- webhook.

Notification failure must not roll back sale.

---

# 110. INTEGRATIONS

Architecture for:

- accounting;
- ERP;
- ecommerce;
- shipping;
- payment;
- tax;
- fiscal;
- CRM;
- BI;
- suppliers;
- marketplaces.

---

# 111. FINANCE

Track enough data for professional business reporting:

- sales;
- tax;
- discounts;
- COGS;
- gross profit;
- cash movements;
- refunds;
- supplier-related financial references;
- store credit.

Do not fake jurisdiction-specific full accounting compliance.

---

# 112. PRIVACY

Support:

- consent;
- retention;
- export;
- anonymization;
- deletion;
- legal preservation.

---

# 113. ACCESSIBILITY

Use:

- keyboard support;
- labels;
- semantic controls;
- screen reader semantics;
- scalable text;
- focus management.

POS should be efficient via keyboard.

---

# 114. CONFIGURATION HIERARCHY

Platform Default
→ Country
→ Organization
→ Business
→ Branch
→ Terminal

Only safe settings may be overridden.

---

# 115. FEATURE AVAILABILITY FORMULA

A feature may depend on:

Permission
AND
Plan entitlement
AND
Country availability
AND
Business configuration
AND
Device capability.

Keep these concepts separate.

---

# 116. ENTITY IDS

Use globally safe identifiers.

Predictable numeric ID is never a security mechanism.

Always authorize resource access.

---

# 117. DOMAIN EVENTS

Possible events:

SaleCompleted
PaymentCaptured
RefundCompleted
StockAdjusted
GoodsReceived
BranchCreated
EmployeeCreated

Do not create event spaghetti.

---

# 118. STATE MACHINES

Define legal state transitions for:

- Sale;
- Payment;
- Refund;
- Purchase Order;
- Subscription;
- Approval;
- Transfer;
- Shift.

Prevent invalid transitions.

---

# 119. SALE / PAYMENT ORCHESTRATION

Do not keep database transaction open while waiting for external network provider.

Persist intermediate state.

Handle uncertain result.

Support reconciliation.

---

# 120. REPORTING ISOLATION

Heavy analytics must not slow down checkout.

Use:

- read replica;
- summary;
- job;
- analytical store later.

---

# 121. MOBILE VERSION COMPATIBILITY

Backend must support defined older app versions.

Do not instantly break users waiting for App Store updates.

---

# 122. DESKTOP UPDATES

Use:

- signed releases;
- integrity checks;
- staged rollout;
- rollback;
- version compatibility.

Do not push risky release instantly to all terminals.

---

# 123. SUPPLY CHAIN SECURITY

Protect CI/CD.

Scan dependencies.

Sign builds where possible.

Do not allow unsigned updater execution.

---

# 124. INCIDENT RESPONSE

Prepare actions for:

- compromised user;
- compromised admin;
- leaked key;
- malicious device;
- suspicious API;
- compromised integration.

Allow:

- revoke tokens;
- disable device;
- disable user;
- rotate secret;
- disable integration.

---

# 125. DOCUMENTATION

Maintain:

- architecture overview;
- domain model;
- module boundaries;
- data flow;
- permission matrix;
- tenancy;
- sync;
- security;
- subscriptions;
- localization;
- hardware;
- API;
- database;
- backup;
- restore;
- DR;
- monitoring;
- runbooks.

---

# 126. ADRs

Significant architecture decisions must get ADRs.

Examples:

- Why modular monolith;
- Why PostgreSQL;
- Why Flutter;
- Why React;
- Why outbox;
- Why offline-first.

---

# 127. CODE QUALITY

CI should run:

- build;
- format;
- lint;
- static analysis;
- tests;
- vulnerability scan;
- secret scan;
- migration checks;
- architecture tests.

---

# 128. DEPENDENCIES

Before adding package, assess:

- security;
- license;
- maintenance;
- necessity;
- compatibility;
- platform support.

---

# 129. PRODUCTION ACCESS

Use least privilege.

Avoid direct production changes.

Emergency access must be:

- limited;
- audited;
- strongly authenticated.

---

# 130. LOGGING

Use structured logs.

Never log:

- plaintext passwords;
- API secrets;
- refresh tokens;
- sensitive payment secrets.

---

# 131. ERROR HANDLING

Do not swallow exceptions.

Provide safe client messages.

Preserve technical trace internally.

Do not leak stack traces.

---

# 132. USER FAILURE MESSAGES

If payment state is unknown:

do not tell user to blindly retry.

If sale is offline:

clearly show pending synchronization.

If sync fails:

do not mark synchronized.

---

# 133. DO NOT OVERENGINEER

Do not add:

- Kubernetes;
- service mesh;
- microservices;
- event sourcing;
- CQRS everywhere;
- sharding;

only because they sound professional.

Use complexity only when justified.

---

# 134. IMPLEMENTATION WORKFLOW FOR CODEX

Before writing major code:

1. inspect entire repository;
2. inspect existing architecture;
3. inspect project files;
4. inspect dependencies;
5. inspect database;
6. inspect migrations;
7. inspect tests;
8. inspect Docker;
9. inspect CI/CD;
10. inspect security configuration;
11. inspect git status;
12. find unfinished work;
13. find fake/mock production logic;
14. find risks;
15. identify reusable code.

Do not blindly overwrite.

---

# 135. FIRST CODEX DELIVERABLE

Create:

## CURRENT STATE ASSESSMENT

Document:

- existing architecture;
- existing stack;
- working features;
- broken features;
- incomplete features;
- security risks;
- reliability risks;
- technical debt.

## TARGET ARCHITECTURE

Document:

- backend;
- frontend;
- client;
- database;
- sync;
- queue;
- cache;
- storage;
- security;
- deployment.

## GAP ANALYSIS

Map current state to target state.

## ROADMAP

Break work into phases.

---

# 136. IMPLEMENTATION PHASES

## PHASE 0 — STABILIZATION

- build;
- dependency verification;
- secrets;
- CI;
- existing tests;
- repository cleanup.

## PHASE 1 — ARCHITECTURE FOUNDATION

- modular boundaries;
- contracts;
- validation;
- error handling;
- logging;
- observability.

## PHASE 2 — IDENTITY/TENANCY

- auth;
- sessions;
- tenant;
- organization;
- business;
- branch;
- permissions;
- Platform Owner.

## PHASE 3 — GLOBAL CONFIGURATION

- localization;
- countries;
- currency;
- timezone;
- taxes;
- locale.

## PHASE 4 — CATALOG

- products;
- variants;
- categories;
- barcodes;
- units.

## PHASE 5 — INVENTORY

- ledger;
- stock;
- locations;
- batches;
- serials;
- counts.

## PHASE 6 — PRICING/PROMOTIONS

- price books;
- discounts;
- promotion engine.

## PHASE 7 — POS

- register;
- shift;
- cart;
- sale;
- cash;
- receipt.

## PHASE 8 — PAYMENTS

- provider abstraction;
- split payment;
- idempotency;
- reconciliation.

## PHASE 9 — OFFLINE/SYNC

- SQLite;
- outbox;
- sync;
- conflict;
- recovery.

## PHASE 10 — PROCUREMENT

- suppliers;
- PO;
- receiving;
- supplier returns.

## PHASE 11 — CRM/LOYALTY

- customers;
- points;
- coupons;
- store credit.

## PHASE 12 — EMPLOYEES/APPROVALS

- roles;
- assignments;
- approval workflows.

## PHASE 13 — REPORTING

- dashboards;
- aggregates;
- exports.

## PHASE 14 — SUBSCRIPTIONS

- plans;
- entitlement;
- limits;
- add-ons;
- trial;
- downgrade.

## PHASE 15 — PLATFORM ADMIN

- Super Admin;
- subscriptions;
- tenant support;
- global monitoring.

## PHASE 16 — INTEGRATIONS

- payments;
- fiscal;
- accounting;
- ecommerce;
- other external systems.

## PHASE 17 — HARDENING

- load;
- stress;
- chaos;
- DR;
- backup;
- restore;
- security;
- performance optimization.

Security is required from Phase 1 onward.

Phase 17 is additional hardening, NOT the first time security is considered.

---

# 137. DEFINITION OF DONE

A feature is not done until relevant requirements are satisfied:

- architecture;
- domain model;
- validation;
- authorization;
- tenant isolation;
- database migration;
- indexes reviewed;
- API;
- client/UI;
- localization;
- error handling;
- logging;
- tracing;
- tests;
- audit;
- idempotency;
- concurrency;
- offline behavior;
- documentation;
- build success;
- security checks.

---

# 138. STOP CONDITIONS

If Codex finds:

- cross-tenant exposure;
- destructive unsafe migration;
- duplicate payment risk;
- permanent data-loss risk;
- authorization vulnerability;
- monetary ambiguity;
- tax/legal ambiguity;
- incompatible architecture;

do NOT blindly continue.

Use the safest reversible approach.

Document the issue.

Do not invent legal rules.

---

# 139. NO FAKE PRODUCTION LOGIC

Do not ship dangerous placeholders such as:

allowAllUsers = true

SkipTenantValidation = true

TODO: security later

Do not mark unfinished code as production ready.

---

# 140. NO FAKE INTEGRATIONS

Clearly distinguish:

- fake;
- mock;
- sandbox;
- production.

Never pretend fake payment/fiscal provider is real.

---

# 141. NO FAKE SUCCESS

Do not return success if:

- persistence failed;
- payment unknown;
- sync failed;
- transaction not durable.

---

# 142. VERSION CONTROL DISCIPLINE

Make logical changes.

Avoid one giant unreviewable commit.

Each meaningful phase should:

- compile;
- pass relevant tests;
- leave repository functional.

Never commit secrets.

---

# 143. FINAL NON-NEGOTIABLE PRINCIPLES

SalekhPos must be:

Secure by default.

Tenant isolated by default.

Durable before acknowledged.

Offline capable for critical workflows.

Idempotent where duplicates are dangerous.

Transactional where atomicity matters.

Auditable where security/business significance exists.

Localized rather than country hardcoded.

Entitlement-driven rather than plan-name hardcoded.

Modular rather than tangled.

Observable rather than opaque.

Recoverable rather than fragile.

Horizontally scalable rather than tied to one server.

Gracefully degrading rather than all-or-nothing.

Simple enough for a tiny store.

Powerful enough for a large international retail chain.

---

# 144. FINAL DIRECTIVE TO CODEX

Read this entire specification before making architectural decisions.

Do not skip requirements.

Do not cherry-pick easy modules.

Do not begin by generating hundreds of random files.

Do not prioritize UI polish over correctness.

Do not implement sales without durable persistence.

Do not implement payments without idempotency.

Do not implement inventory without traceable movements.

Do not implement authentication without authorization.

Do not implement authorization without tenant isolation.

Do not implement global support by only translating buttons.

Do not implement offline mode without a real synchronization protocol.

Do not implement backup without restore testing.

Do not claim scalability without load testing.

Do not claim security without security controls and tests.

Do not claim reliability without redundancy, recovery and observability.

First inspect the existing repository.

Then produce:

1. Current State Assessment.
2. Target Architecture.
3. Gap Analysis.
4. Implementation Roadmap.

Then implement SalekhPos phase by phase.

After every phase:

1. build;
2. run tests;
3. inspect authorization;
4. inspect tenant isolation;
5. inspect migrations;
6. inspect logging for secrets;
7. inspect data integrity;
8. inspect performance implications;
9. update documentation;
10. document unresolved risks.

Never knowingly leave the repository broken.

The objective is not maximum code volume.

The objective is to build the most professional, secure, reliable, global, scalable and maintainable SalekhPos platform possible.