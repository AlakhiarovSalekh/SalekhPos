```text
SalekhPos/
│
├── .github/
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.yml
│   │   ├── feature_request.yml
│   │   ├── security_report.yml
│   │   └── config.yml
│   │
│   ├── workflows/
│   │   ├── ci.yml
│   │   ├── backend-ci.yml
│   │   ├── web-ci.yml
│   │   ├── desktop-ci.yml
│   │   ├── mobile-ci.yml
│   │   ├── unit-tests.yml
│   │   ├── integration-tests.yml
│   │   ├── contract-tests.yml
│   │   ├── e2e-tests.yml
│   │   ├── performance-tests.yml
│   │   ├── security-scan.yml
│   │   ├── dependency-scan.yml
│   │   ├── container-scan.yml
│   │   ├── database-migration-check.yml
│   │   ├── deploy-development.yml
│   │   ├── deploy-staging.yml
│   │   ├── deploy-production.yml
│   │   └── release.yml
│   │
│   ├── CODEOWNERS
│   ├── dependabot.yml
│   └── PULL_REQUEST_TEMPLATE.md
│
├── .config/
│   ├── dotnet-tools.json
│   ├── eslint/
│   ├── prettier/
│   └── editor/
│
├── .devcontainer/
│   ├── devcontainer.json
│   └── Dockerfile
│
├── .vscode/
│   ├── extensions.json
│   ├── launch.json
│   ├── settings.json
│   └── tasks.json
│
├── apps/
│   │
│   ├── web/
│   │   ├── public/
│   │   │   ├── assets/
│   │   │   ├── icons/
│   │   │   ├── images/
│   │   │   └── manifest/
│   │   │
│   │   ├── src/
│   │   │   ├── app/
│   │   │   │   ├── (public)/
│   │   │   │   │   ├── page.tsx
│   │   │   │   │   ├── pricing/
│   │   │   │   │   ├── features/
│   │   │   │   │   ├── contact/
│   │   │   │   │   ├── help/
│   │   │   │   │   └── legal/
│   │   │   │   │       ├── privacy/
│   │   │   │   │       ├── terms/
│   │   │   │   │       └── cookies/
│   │   │   │   │
│   │   │   │   ├── (auth)/
│   │   │   │   │   ├── sign-in/
│   │   │   │   │   ├── sign-up/
│   │   │   │   │   ├── verify-email/
│   │   │   │   │   ├── forgot-password/
│   │   │   │   │   ├── reset-password/
│   │   │   │   │   ├── mfa/
│   │   │   │   │   ├── recovery/
│   │   │   │   │   └── invitation/
│   │   │   │   │
│   │   │   │   ├── (dashboard)/
│   │   │   │   │   ├── dashboard/
│   │   │   │   │   ├── organizations/
│   │   │   │   │   ├── stores/
│   │   │   │   │   ├── registers/
│   │   │   │   │   ├── devices/
│   │   │   │   │   ├── sales/
│   │   │   │   │   ├── returns/
│   │   │   │   │   ├── products/
│   │   │   │   │   ├── categories/
│   │   │   │   │   ├── brands/
│   │   │   │   │   ├── pricing/
│   │   │   │   │   ├── promotions/
│   │   │   │   │   ├── inventory/
│   │   │   │   │   ├── stock-counts/
│   │   │   │   │   ├── stock-adjustments/
│   │   │   │   │   ├── stock-transfers/
│   │   │   │   │   ├── warehouses/
│   │   │   │   │   ├── suppliers/
│   │   │   │   │   ├── purchasing/
│   │   │   │   │   ├── customers/
│   │   │   │   │   ├── loyalty/
│   │   │   │   │   ├── employees/
│   │   │   │   │   ├── roles/
│   │   │   │   │   ├── permissions/
│   │   │   │   │   ├── shifts/
│   │   │   │   │   ├── cash-management/
│   │   │   │   │   ├── payments/
│   │   │   │   │   ├── accounting/
│   │   │   │   │   ├── taxation/
│   │   │   │   │   ├── fiscalization/
│   │   │   │   │   ├── reports/
│   │   │   │   │   ├── analytics/
│   │   │   │   │   ├── notifications/
│   │   │   │   │   ├── integrations/
│   │   │   │   │   ├── subscriptions/
│   │   │   │   │   ├── billing/
│   │   │   │   │   ├── audit-log/
│   │   │   │   │   ├── security/
│   │   │   │   │   ├── support/
│   │   │   │   │   └── settings/
│   │   │   │   │
│   │   │   │   └── super-admin/
│   │   │   │       ├── overview/
│   │   │   │       ├── tenants/
│   │   │   │       ├── organizations/
│   │   │   │       ├── stores/
│   │   │   │       ├── users/
│   │   │   │       ├── super-admins/
│   │   │   │       ├── subscriptions/
│   │   │   │       ├── billing/
│   │   │   │       ├── payments/
│   │   │   │       ├── plans/
│   │   │   │       ├── entitlements/
│   │   │   │       ├── feature-flags/
│   │   │   │       ├── countries/
│   │   │   │       ├── system-health/
│   │   │   │       ├── services/
│   │   │   │       ├── jobs/
│   │   │   │       ├── incidents/
│   │   │   │       ├── integrations/
│   │   │   │       ├── security/
│   │   │   │       ├── audit/
│   │   │   │       ├── support/
│   │   │   │       └── configuration/
│   │   │   │
│   │   │   ├── components/
│   │   │   │   ├── ui/
│   │   │   │   ├── forms/
│   │   │   │   ├── tables/
│   │   │   │   ├── charts/
│   │   │   │   ├── filters/
│   │   │   │   ├── navigation/
│   │   │   │   ├── dialogs/
│   │   │   │   ├── feedback/
│   │   │   │   ├── layouts/
│   │   │   │   └── domain/
│   │   │   │
│   │   │   ├── features/
│   │   │   │   ├── auth/
│   │   │   │   ├── organizations/
│   │   │   │   ├── stores/
│   │   │   │   ├── sales/
│   │   │   │   ├── products/
│   │   │   │   ├── inventory/
│   │   │   │   ├── purchasing/
│   │   │   │   ├── customers/
│   │   │   │   ├── employees/
│   │   │   │   ├── shifts/
│   │   │   │   ├── reporting/
│   │   │   │   ├── billing/
│   │   │   │   └── settings/
│   │   │   │
│   │   │   ├── api/
│   │   │   ├── hooks/
│   │   │   ├── lib/
│   │   │   ├── providers/
│   │   │   ├── services/
│   │   │   ├── state/
│   │   │   ├── types/
│   │   │   ├── validation/
│   │   │   ├── constants/
│   │   │   ├── config/
│   │   │   ├── i18n/
│   │   │   ├── permissions/
│   │   │   ├── telemetry/
│   │   │   └── middleware/
│   │   │
│   │   ├── tests/
│   │   │   ├── unit/
│   │   │   ├── integration/
│   │   │   └── e2e/
│   │   │
│   │   ├── package.json
│   │   ├── next.config.ts
│   │   ├── tsconfig.json
│   │   └── eslint.config.js
│   │
│   ├── desktop/
│   │   ├── src/
│   │   │   ├── SalekhPos.Desktop/
│   │   │   │   ├── App.axaml
│   │   │   │   ├── App.axaml.cs
│   │   │   │   ├── Program.cs
│   │   │   │   │
│   │   │   │   ├── Views/
│   │   │   │   │   ├── Authentication/
│   │   │   │   │   ├── Setup/
│   │   │   │   │   ├── POS/
│   │   │   │   │   ├── Sales/
│   │   │   │   │   ├── Returns/
│   │   │   │   │   ├── Products/
│   │   │   │   │   ├── Inventory/
│   │   │   │   │   ├── StockReceiving/
│   │   │   │   │   ├── StockCounts/
│   │   │   │   │   ├── Customers/
│   │   │   │   │   ├── Shifts/
│   │   │   │   │   ├── CashManagement/
│   │   │   │   │   ├── Reports/
│   │   │   │   │   ├── Devices/
│   │   │   │   │   ├── Settings/
│   │   │   │   │   ├── Offline/
│   │   │   │   │   └── Sync/
│   │   │   │   │
│   │   │   │   ├── ViewModels/
│   │   │   │   ├── Controls/
│   │   │   │   ├── Converters/
│   │   │   │   ├── Behaviors/
│   │   │   │   ├── Navigation/
│   │   │   │   ├── Themes/
│   │   │   │   ├── Styles/
│   │   │   │   ├── Assets/
│   │   │   │   ├── Localization/
│   │   │   │   └── DependencyInjection/
│   │   │   │
│   │   │   ├── SalekhPos.Desktop.Application/
│   │   │   │   ├── Authentication/
│   │   │   │   ├── DeviceRegistration/
│   │   │   │   ├── POS/
│   │   │   │   ├── Sales/
│   │   │   │   ├── Returns/
│   │   │   │   ├── Inventory/
│   │   │   │   ├── Customers/
│   │   │   │   ├── Shifts/
│   │   │   │   ├── CashManagement/
│   │   │   │   ├── Offline/
│   │   │   │   ├── Sync/
│   │   │   │   └── Hardware/
│   │   │   │
│   │   │   ├── SalekhPos.Desktop.Domain/
│   │   │   │   ├── LocalSales/
│   │   │   │   ├── LocalInventory/
│   │   │   │   ├── Sync/
│   │   │   │   ├── Devices/
│   │   │   │   └── Configuration/
│   │   │   │
│   │   │   └── SalekhPos.Desktop.Infrastructure/
│   │   │       ├── Api/
│   │   │       ├── Authentication/
│   │   │       ├── LocalDatabase/
│   │   │       │   ├── Context/
│   │   │       │   ├── Entities/
│   │   │       │   ├── Configurations/
│   │   │       │   ├── Migrations/
│   │   │       │   └── Repositories/
│   │   │       │
│   │   │       ├── Offline/
│   │   │       │   ├── Outbox/
│   │   │       │   ├── Inbox/
│   │   │       │   ├── Queue/
│   │   │       │   └── Checkpoints/
│   │   │       │
│   │   │       ├── Sync/
│   │   │       │   ├── Upload/
│   │   │       │   ├── Download/
│   │   │       │   ├── Retry/
│   │   │       │   ├── ConflictResolution/
│   │   │       │   └── Versioning/
│   │   │       │
│   │   │       ├── Hardware/
│   │   │       │   ├── Abstractions/
│   │   │       │   │   ├── IReceiptPrinter.cs
│   │   │       │   │   ├── IBarcodeScanner.cs
│   │   │       │   │   ├── ICashDrawer.cs
│   │   │       │   │   ├── IScale.cs
│   │   │       │   │   ├── IPaymentTerminal.cs
│   │   │       │   │   └── ICustomerDisplay.cs
│   │   │       │   │
│   │   │       │   ├── Printers/
│   │   │       │   │   ├── GenericEscPos/
│   │   │       │   │   ├── Epson/
│   │   │       │   │   ├── Star/
│   │   │       │   │   └── Bixolon/
│   │   │       │   │
│   │   │       │   ├── BarcodeScanners/
│   │   │       │   ├── CashDrawers/
│   │   │       │   ├── Scales/
│   │   │       │   ├── PaymentTerminals/
│   │   │       │   └── CustomerDisplays/
│   │   │       │
│   │   │       ├── Printing/
│   │   │       ├── Storage/
│   │   │       ├── Security/
│   │   │       ├── Telemetry/
│   │   │       └── Configuration/
│   │   │
│   │   ├── tests/
│   │   │   ├── Unit/
│   │   │   ├── Integration/
│   │   │   ├── Hardware/
│   │   │   ├── Offline/
│   │   │   └── Sync/
│   │   │
│   │   └── SalekhPos.Desktop.sln
│   │
│   ├── mobile/
│   │   ├── android/
│   │   ├── ios/
│   │   │
│   │   ├── src/
│   │   │   ├── app/
│   │   │   ├── screens/
│   │   │   │   ├── authentication/
│   │   │   │   ├── dashboard/
│   │   │   │   ├── organizations/
│   │   │   │   ├── stores/
│   │   │   │   ├── sales/
│   │   │   │   ├── products/
│   │   │   │   ├── inventory/
│   │   │   │   ├── stock-receiving/
│   │   │   │   ├── stock-count/
│   │   │   │   ├── stock-transfer/
│   │   │   │   ├── barcode-scanner/
│   │   │   │   ├── customers/
│   │   │   │   ├── employees/
│   │   │   │   ├── approvals/
│   │   │   │   ├── reports/
│   │   │   │   ├── notifications/
│   │   │   │   ├── devices/
│   │   │   │   └── settings/
│   │   │   │
│   │   │   ├── components/
│   │   │   ├── features/
│   │   │   ├── navigation/
│   │   │   ├── services/
│   │   │   ├── api/
│   │   │   ├── state/
│   │   │   ├── hooks/
│   │   │   ├── storage/
│   │   │   ├── offline/
│   │   │   ├── sync/
│   │   │   ├── permissions/
│   │   │   ├── localization/
│   │   │   ├── notifications/
│   │   │   ├── security/
│   │   │   ├── telemetry/
│   │   │   └── config/
│   │   │
│   │   ├── tests/
│   │   │   ├── unit/
│   │   │   ├── integration/
│   │   │   └── e2e/
│   │   │
│   │   ├── package.json
│   │   └── tsconfig.json
│   │
│   └── kiosk/
│       ├── src/
│       │   ├── app/
│       │   ├── screens/
│       │   ├── components/
│       │   ├── cart/
│       │   ├── catalog/
│       │   ├── payments/
│       │   ├── receipts/
│       │   ├── hardware/
│       │   ├── localization/
│       │   ├── security/
│       │   └── config/
│       ├── public/
│       ├── tests/
│       └── package.json
│
├── backend/
│   │
│   ├── src/
│   │   │
│   │   ├── Bootstrapper/
│   │   │   ├── SalekhPos.Api/
│   │   │   │   ├── Endpoints/
│   │   │   │   ├── Middleware/
│   │   │   │   ├── Filters/
│   │   │   │   ├── Authentication/
│   │   │   │   ├── Authorization/
│   │   │   │   ├── OpenApi/
│   │   │   │   ├── Versioning/
│   │   │   │   ├── HealthChecks/
│   │   │   │   ├── Configuration/
│   │   │   │   ├── DependencyInjection/
│   │   │   │   ├── ExceptionHandling/
│   │   │   │   └── Program.cs
│   │   │   │
│   │   │   ├── SalekhPos.Worker/
│   │   │   │   ├── Consumers/
│   │   │   │   ├── Jobs/
│   │   │   │   ├── Schedulers/
│   │   │   │   ├── Dispatchers/
│   │   │   │   ├── HealthChecks/
│   │   │   │   ├── DependencyInjection/
│   │   │   │   └── Program.cs
│   │   │   │
│   │   │   └── SalekhPos.Migrations/
│   │   │       ├── MigrationRunner/
│   │   │       └── Program.cs
│   │   │
│   │   ├── BuildingBlocks/
│   │   │   ├── SalekhPos.SharedKernel/
│   │   │   │   ├── Domain/
│   │   │   │   │   ├── Entity.cs
│   │   │   │   │   ├── AggregateRoot.cs
│   │   │   │   │   ├── ValueObject.cs
│   │   │   │   │   ├── DomainEvent.cs
│   │   │   │   │   └── AuditableEntity.cs
│   │   │   │   ├── Money/
│   │   │   │   ├── Time/
│   │   │   │   ├── Results/
│   │   │   │   ├── Errors/
│   │   │   │   ├── Exceptions/
│   │   │   │   ├── Identifiers/
│   │   │   │   ├── Primitives/
│   │   │   │   └── Abstractions/
│   │   │   │
│   │   │   ├── SalekhPos.Application/
│   │   │   │   ├── CQRS/
│   │   │   │   ├── Messaging/
│   │   │   │   ├── Validation/
│   │   │   │   ├── Behaviors/
│   │   │   │   ├── Transactions/
│   │   │   │   ├── Authorization/
│   │   │   │   ├── Idempotency/
│   │   │   │   └── Abstractions/
│   │   │   │
│   │   │   ├── SalekhPos.Infrastructure/
│   │   │   │   ├── Persistence/
│   │   │   │   ├── Messaging/
│   │   │   │   ├── Outbox/
│   │   │   │   ├── Inbox/
│   │   │   │   ├── Caching/
│   │   │   │   ├── Security/
│   │   │   │   ├── Authentication/
│   │   │   │   ├── Authorization/
│   │   │   │   ├── Storage/
│   │   │   │   ├── Email/
│   │   │   │   ├── Sms/
│   │   │   │   ├── Push/
│   │   │   │   ├── Observability/
│   │   │   │   ├── Resilience/
│   │   │   │   ├── FeatureFlags/
│   │   │   │   └── Configuration/
│   │   │   │
│   │   │   └── SalekhPos.Contracts/
│   │   │       ├── Api/
│   │   │       ├── Events/
│   │   │       ├── Commands/
│   │   │       ├── Queries/
│   │   │       ├── DTOs/
│   │   │       ├── Errors/
│   │   │       └── Versioning/
│   │   │
│   │   └── Modules/
│   │       │
│   │       ├── Identity/
│   │       │   ├── SalekhPos.Identity.Domain/
│   │       │   │   ├── Users/
│   │       │   │   ├── Credentials/
│   │       │   │   ├── Sessions/
│   │       │   │   ├── MFA/
│   │       │   │   ├── Passkeys/
│   │       │   │   ├── Recovery/
│   │       │   │   ├── SecurityEvents/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Identity.Application/
│   │       │   │   ├── SignIn/
│   │       │   │   ├── SignOut/
│   │       │   │   ├── Registration/
│   │       │   │   ├── VerifyEmail/
│   │       │   │   ├── PasswordReset/
│   │       │   │   ├── MFA/
│   │       │   │   ├── Sessions/
│   │       │   │   └── Recovery/
│   │       │   ├── SalekhPos.Identity.Infrastructure/
│   │       │   │   ├── Persistence/
│   │       │   │   ├── Authentication/
│   │       │   │   ├── Tokens/
│   │       │   │   ├── PasswordHashing/
│   │       │   │   ├── EmailVerification/
│   │       │   │   └── Repositories/
│   │       │   ├── SalekhPos.Identity.Api/
│   │       │   │   └── Endpoints/
│   │       │   └── SalekhPos.Identity.Contracts/
│   │       │
│   │       ├── Tenancy/
│   │       │   ├── SalekhPos.Tenancy.Domain/
│   │       │   │   ├── Tenants/
│   │       │   │   ├── TenantStatus/
│   │       │   │   ├── TenantContext/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Tenancy.Application/
│   │       │   ├── SalekhPos.Tenancy.Infrastructure/
│   │       │   ├── SalekhPos.Tenancy.Api/
│   │       │   └── SalekhPos.Tenancy.Contracts/
│   │       │
│   │       ├── Organizations/
│   │       │   ├── SalekhPos.Organizations.Domain/
│   │       │   │   ├── Organizations/
│   │       │   │   ├── LegalProfiles/
│   │       │   │   ├── BusinessProfiles/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Organizations.Application/
│   │       │   ├── SalekhPos.Organizations.Infrastructure/
│   │       │   ├── SalekhPos.Organizations.Api/
│   │       │   └── SalekhPos.Organizations.Contracts/
│   │       │
│   │       ├── Stores/
│   │       │   ├── SalekhPos.Stores.Domain/
│   │       │   │   ├── Stores/
│   │       │   │   ├── StoreSettings/
│   │       │   │   ├── Registers/
│   │       │   │   ├── BusinessHours/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Stores.Application/
│   │       │   ├── SalekhPos.Stores.Infrastructure/
│   │       │   ├── SalekhPos.Stores.Api/
│   │       │   └── SalekhPos.Stores.Contracts/
│   │       │
│   │       ├── Authorization/
│   │       │   ├── SalekhPos.Authorization.Domain/
│   │       │   │   ├── Roles/
│   │       │   │   ├── Permissions/
│   │       │   │   ├── Policies/
│   │       │   │   ├── Scopes/
│   │       │   │   ├── Assignments/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Authorization.Application/
│   │       │   ├── SalekhPos.Authorization.Infrastructure/
│   │       │   ├── SalekhPos.Authorization.Api/
│   │       │   └── SalekhPos.Authorization.Contracts/
│   │       │
│   │       ├── Employees/
│   │       │   ├── SalekhPos.Employees.Domain/
│   │       │   │   ├── Employees/
│   │       │   │   ├── Employment/
│   │       │   │   ├── StoreAssignments/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Employees.Application/
│   │       │   ├── SalekhPos.Employees.Infrastructure/
│   │       │   ├── SalekhPos.Employees.Api/
│   │       │   └── SalekhPos.Employees.Contracts/
│   │       │
│   │       ├── Devices/
│   │       │   ├── SalekhPos.Devices.Domain/
│   │       │   │   ├── Devices/
│   │       │   │   ├── DeviceRegistration/
│   │       │   │   ├── DeviceTrust/
│   │       │   │   ├── Versions/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Devices.Application/
│   │       │   ├── SalekhPos.Devices.Infrastructure/
│   │       │   ├── SalekhPos.Devices.Api/
│   │       │   └── SalekhPos.Devices.Contracts/
│   │       │
│   │       ├── Catalog/
│   │       │   ├── SalekhPos.Catalog.Domain/
│   │       │   │   ├── Products/
│   │       │   │   ├── Variants/
│   │       │   │   ├── Categories/
│   │       │   │   ├── Brands/
│   │       │   │   ├── SKUs/
│   │       │   │   ├── Barcodes/
│   │       │   │   ├── Units/
│   │       │   │   ├── Attributes/
│   │       │   │   ├── Bundles/
│   │       │   │   ├── ProductImages/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Catalog.Application/
│   │       │   ├── SalekhPos.Catalog.Infrastructure/
│   │       │   ├── SalekhPos.Catalog.Api/
│   │       │   └── SalekhPos.Catalog.Contracts/
│   │       │
│   │       ├── Pricing/
│   │       │   ├── SalekhPos.Pricing.Domain/
│   │       │   │   ├── Prices/
│   │       │   │   ├── PriceLists/
│   │       │   │   ├── StorePricing/
│   │       │   │   ├── ScheduledPricing/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Pricing.Application/
│   │       │   ├── SalekhPos.Pricing.Infrastructure/
│   │       │   ├── SalekhPos.Pricing.Api/
│   │       │   └── SalekhPos.Pricing.Contracts/
│   │       │
│   │       ├── Promotions/
│   │       │   ├── SalekhPos.Promotions.Domain/
│   │       │   │   ├── Promotions/
│   │       │   │   ├── DiscountRules/
│   │       │   │   ├── Conditions/
│   │       │   │   ├── Rewards/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Promotions.Application/
│   │       │   ├── SalekhPos.Promotions.Infrastructure/
│   │       │   ├── SalekhPos.Promotions.Api/
│   │       │   └── SalekhPos.Promotions.Contracts/
│   │       │
│   │       ├── Inventory/
│   │       │   ├── SalekhPos.Inventory.Domain/
│   │       │   │   ├── Stock/
│   │       │   │   ├── StockLedger/
│   │       │   │   ├── StockMovements/
│   │       │   │   ├── StockAdjustments/
│   │       │   │   ├── StockReservations/
│   │       │   │   ├── StockCounts/
│   │       │   │   ├── Transfers/
│   │       │   │   ├── Lots/
│   │       │   │   ├── Batches/
│   │       │   │   ├── SerialNumbers/
│   │       │   │   ├── Expiration/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Inventory.Application/
│   │       │   │   ├── ReceiveStock/
│   │       │   │   ├── AdjustStock/
│   │       │   │   ├── TransferStock/
│   │       │   │   ├── CountStock/
│   │       │   │   └── QueryStock/
│   │       │   ├── SalekhPos.Inventory.Infrastructure/
│   │       │   ├── SalekhPos.Inventory.Api/
│   │       │   └── SalekhPos.Inventory.Contracts/
│   │       │
│   │       ├── Warehousing/
│   │       │   ├── SalekhPos.Warehousing.Domain/
│   │       │   │   ├── Warehouses/
│   │       │   │   ├── Locations/
│   │       │   │   ├── Receiving/
│   │       │   │   ├── Dispatch/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Warehousing.Application/
│   │       │   ├── SalekhPos.Warehousing.Infrastructure/
│   │       │   ├── SalekhPos.Warehousing.Api/
│   │       │   └── SalekhPos.Warehousing.Contracts/
│   │       │
│   │       ├── Suppliers/
│   │       │   ├── SalekhPos.Suppliers.Domain/
│   │       │   │   ├── Suppliers/
│   │       │   │   ├── Contacts/
│   │       │   │   ├── Addresses/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Suppliers.Application/
│   │       │   ├── SalekhPos.Suppliers.Infrastructure/
│   │       │   ├── SalekhPos.Suppliers.Api/
│   │       │   └── SalekhPos.Suppliers.Contracts/
│   │       │
│   │       ├── Purchasing/
│   │       │   ├── SalekhPos.Purchasing.Domain/
│   │       │   │   ├── PurchaseOrders/
│   │       │   │   ├── PurchaseOrderItems/
│   │       │   │   ├── Receiving/
│   │       │   │   ├── SupplierInvoices/
│   │       │   │   ├── PurchaseReturns/
│   │       │   │   ├── Costs/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Purchasing.Application/
│   │       │   ├── SalekhPos.Purchasing.Infrastructure/
│   │       │   ├── SalekhPos.Purchasing.Api/
│   │       │   └── SalekhPos.Purchasing.Contracts/
│   │       │
│   │       ├── Sales/
│   │       │   ├── SalekhPos.Sales.Domain/
│   │       │   │   ├── Carts/
│   │       │   │   ├── Sales/
│   │       │   │   ├── SaleItems/
│   │       │   │   ├── Discounts/
│   │       │   │   ├── AppliedTaxes/
│   │       │   │   ├── Receipts/
│   │       │   │   ├── Invoices/
│   │       │   │   ├── SuspendedSales/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Sales.Application/
│   │       │   │   ├── CreateSale/
│   │       │   │   ├── CompleteSale/
│   │       │   │   ├── SuspendSale/
│   │       │   │   ├── ResumeSale/
│   │       │   │   ├── VoidSale/
│   │       │   │   └── Queries/
│   │       │   ├── SalekhPos.Sales.Infrastructure/
│   │       │   ├── SalekhPos.Sales.Api/
│   │       │   └── SalekhPos.Sales.Contracts/
│   │       │
│   │       ├── Payments/
│   │       │   ├── SalekhPos.Payments.Domain/
│   │       │   │   ├── Payments/
│   │       │   │   ├── PaymentMethods/
│   │       │   │   ├── Refunds/
│   │       │   │   ├── Settlements/
│   │       │   │   ├── Reconciliation/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Payments.Application/
│   │       │   ├── SalekhPos.Payments.Infrastructure/
│   │       │   ├── SalekhPos.Payments.Api/
│   │       │   └── SalekhPos.Payments.Contracts/
│   │       │
│   │       ├── Returns/
│   │       │   ├── SalekhPos.Returns.Domain/
│   │       │   │   ├── Returns/
│   │       │   │   ├── ReturnItems/
│   │       │   │   ├── ReturnReasons/
│   │       │   │   ├── Restocking/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Returns.Application/
│   │       │   ├── SalekhPos.Returns.Infrastructure/
│   │       │   ├── SalekhPos.Returns.Api/
│   │       │   └── SalekhPos.Returns.Contracts/
│   │       │
│   │       ├── CashManagement/
│   │       │   ├── SalekhPos.CashManagement.Domain/
│   │       │   │   ├── CashDrawers/
│   │       │   │   ├── CashMovements/
│   │       │   │   ├── CashIn/
│   │       │   │   ├── CashOut/
│   │       │   │   ├── Reconciliation/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.CashManagement.Application/
│   │       │   ├── SalekhPos.CashManagement.Infrastructure/
│   │       │   ├── SalekhPos.CashManagement.Api/
│   │       │   └── SalekhPos.CashManagement.Contracts/
│   │       │
│   │       ├── ShiftManagement/
│   │       │   ├── SalekhPos.ShiftManagement.Domain/
│   │       │   │   ├── Shifts/
│   │       │   │   ├── ShiftOpening/
│   │       │   │   ├── ShiftClosing/
│   │       │   │   ├── Balances/
│   │       │   │   ├── Variances/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.ShiftManagement.Application/
│   │       │   ├── SalekhPos.ShiftManagement.Infrastructure/
│   │       │   ├── SalekhPos.ShiftManagement.Api/
│   │       │   └── SalekhPos.ShiftManagement.Contracts/
│   │       │
│   │       ├── Customers/
│   │       │   ├── SalekhPos.Customers.Domain/
│   │       │   │   ├── Customers/
│   │       │   │   ├── ContactInformation/
│   │       │   │   ├── Addresses/
│   │       │   │   ├── Preferences/
│   │       │   │   ├── Consent/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Customers.Application/
│   │       │   ├── SalekhPos.Customers.Infrastructure/
│   │       │   ├── SalekhPos.Customers.Api/
│   │       │   └── SalekhPos.Customers.Contracts/
│   │       │
│   │       ├── Loyalty/
│   │       │   ├── SalekhPos.Loyalty.Domain/
│   │       │   │   ├── Accounts/
│   │       │   │   ├── Points/
│   │       │   │   ├── Rewards/
│   │       │   │   ├── Tiers/
│   │       │   │   ├── Expiration/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Loyalty.Application/
│   │       │   ├── SalekhPos.Loyalty.Infrastructure/
│   │       │   ├── SalekhPos.Loyalty.Api/
│   │       │   └── SalekhPos.Loyalty.Contracts/
│   │       │
│   │       ├── Accounting/
│   │       │   ├── SalekhPos.Accounting.Domain/
│   │       │   │   ├── Journals/
│   │       │   │   ├── Entries/
│   │       │   │   ├── Reconciliation/
│   │       │   │   ├── Exports/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Accounting.Application/
│   │       │   ├── SalekhPos.Accounting.Infrastructure/
│   │       │   ├── SalekhPos.Accounting.Api/
│   │       │   └── SalekhPos.Accounting.Contracts/
│   │       │
│   │       ├── Taxation/
│   │       │   ├── SalekhPos.Taxation.Domain/
│   │       │   │   ├── TaxRates/
│   │       │   │   ├── TaxCategories/
│   │       │   │   ├── TaxProfiles/
│   │       │   │   ├── Exemptions/
│   │       │   │   ├── Calculations/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Taxation.Application/
│   │       │   ├── SalekhPos.Taxation.Infrastructure/
│   │       │   ├── SalekhPos.Taxation.Api/
│   │       │   └── SalekhPos.Taxation.Contracts/
│   │       │
│   │       ├── Fiscalization/
│   │       │   ├── SalekhPos.Fiscalization.Domain/
│   │       │   │   ├── FiscalDocuments/
│   │       │   │   ├── FiscalStatus/
│   │       │   │   ├── Providers/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Fiscalization.Application/
│   │       │   ├── SalekhPos.Fiscalization.Infrastructure/
│   │       │   ├── SalekhPos.Fiscalization.Api/
│   │       │   └── SalekhPos.Fiscalization.Contracts/
│   │       │
│   │       ├── Reporting/
│   │       │   ├── SalekhPos.Reporting.Domain/
│   │       │   ├── SalekhPos.Reporting.Application/
│   │       │   │   ├── SalesReports/
│   │       │   │   ├── InventoryReports/
│   │       │   │   ├── EmployeeReports/
│   │       │   │   ├── TaxReports/
│   │       │   │   ├── CashReports/
│   │       │   │   ├── PurchaseReports/
│   │       │   │   └── Exports/
│   │       │   ├── SalekhPos.Reporting.Infrastructure/
│   │       │   ├── SalekhPos.Reporting.Api/
│   │       │   └── SalekhPos.Reporting.Contracts/
│   │       │
│   │       ├── Analytics/
│   │       │   ├── SalekhPos.Analytics.Domain/
│   │       │   ├── SalekhPos.Analytics.Application/
│   │       │   │   ├── KPIs/
│   │       │   │   ├── SalesTrends/
│   │       │   │   ├── StoreComparison/
│   │       │   │   ├── InventoryTurnover/
│   │       │   │   └── CustomerAnalytics/
│   │       │   ├── SalekhPos.Analytics.Infrastructure/
│   │       │   ├── SalekhPos.Analytics.Api/
│   │       │   └── SalekhPos.Analytics.Contracts/
│   │       │
│   │       ├── Notifications/
│   │       │   ├── SalekhPos.Notifications.Domain/
│   │       │   │   ├── Notifications/
│   │       │   │   ├── Channels/
│   │       │   │   ├── Templates/
│   │       │   │   ├── Preferences/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Notifications.Application/
│   │       │   ├── SalekhPos.Notifications.Infrastructure/
│   │       │   │   ├── Email/
│   │       │   │   ├── Sms/
│   │       │   │   ├── Push/
│   │       │   │   └── InApp/
│   │       │   ├── SalekhPos.Notifications.Api/
│   │       │   └── SalekhPos.Notifications.Contracts/
│   │       │
│   │       ├── Audit/
│   │       │   ├── SalekhPos.Audit.Domain/
│   │       │   │   ├── AuditEvents/
│   │       │   │   ├── AuditActors/
│   │       │   │   ├── AuditTargets/
│   │       │   │   └── Retention/
│   │       │   ├── SalekhPos.Audit.Application/
│   │       │   ├── SalekhPos.Audit.Infrastructure/
│   │       │   ├── SalekhPos.Audit.Api/
│   │       │   └── SalekhPos.Audit.Contracts/
│   │       │
│   │       ├── Billing/
│   │       │   ├── SalekhPos.Billing.Domain/
│   │       │   │   ├── Accounts/
│   │       │   │   ├── Invoices/
│   │       │   │   ├── Charges/
│   │       │   │   ├── Credits/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Billing.Application/
│   │       │   ├── SalekhPos.Billing.Infrastructure/
│   │       │   ├── SalekhPos.Billing.Api/
│   │       │   └── SalekhPos.Billing.Contracts/
│   │       │
│   │       ├── Subscriptions/
│   │       │   ├── SalekhPos.Subscriptions.Domain/
│   │       │   │   ├── Plans/
│   │       │   │   ├── Subscriptions/
│   │       │   │   ├── Entitlements/
│   │       │   │   ├── Trials/
│   │       │   │   ├── Renewals/
│   │       │   │   ├── Cancellations/
│   │       │   │   ├── Usage/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Subscriptions.Application/
│   │       │   ├── SalekhPos.Subscriptions.Infrastructure/
│   │       │   ├── SalekhPos.Subscriptions.Api/
│   │       │   └── SalekhPos.Subscriptions.Contracts/
│   │       │
│   │       ├── Integrations/
│   │       │   ├── SalekhPos.Integrations.Domain/
│   │       │   │   ├── IntegrationConnections/
│   │       │   │   ├── Credentials/
│   │       │   │   ├── Webhooks/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Integrations.Application/
│   │       │   ├── SalekhPos.Integrations.Infrastructure/
│   │       │   ├── SalekhPos.Integrations.Api/
│   │       │   └── SalekhPos.Integrations.Contracts/
│   │       │
│   │       ├── Sync/
│   │       │   ├── SalekhPos.Sync.Domain/
│   │       │   │   ├── SyncSessions/
│   │       │   │   ├── Checkpoints/
│   │       │   │   ├── Conflicts/
│   │       │   │   ├── SyncMessages/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Sync.Application/
│   │       │   ├── SalekhPos.Sync.Infrastructure/
│   │       │   ├── SalekhPos.Sync.Api/
│   │       │   └── SalekhPos.Sync.Contracts/
│   │       │
│   │       ├── Localization/
│   │       │   ├── SalekhPos.Localization.Domain/
│   │       │   │   ├── Languages/
│   │       │   │   ├── Locales/
│   │       │   │   ├── Currencies/
│   │       │   │   ├── Timezones/
│   │       │   │   └── CountrySettings/
│   │       │   ├── SalekhPos.Localization.Application/
│   │       │   ├── SalekhPos.Localization.Infrastructure/
│   │       │   ├── SalekhPos.Localization.Api/
│   │       │   └── SalekhPos.Localization.Contracts/
│   │       │
│   │       ├── FeatureManagement/
│   │       │   ├── SalekhPos.FeatureManagement.Domain/
│   │       │   │   ├── Features/
│   │       │   │   ├── Flags/
│   │       │   │   ├── Entitlements/
│   │       │   │   ├── Rollouts/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.FeatureManagement.Application/
│   │       │   ├── SalekhPos.FeatureManagement.Infrastructure/
│   │       │   ├── SalekhPos.FeatureManagement.Api/
│   │       │   └── SalekhPos.FeatureManagement.Contracts/
│   │       │
│   │       ├── Support/
│   │       │   ├── SalekhPos.Support.Domain/
│   │       │   │   ├── Tickets/
│   │       │   │   ├── Cases/
│   │       │   │   ├── Diagnostics/
│   │       │   │   └── Events/
│   │       │   ├── SalekhPos.Support.Application/
│   │       │   ├── SalekhPos.Support.Infrastructure/
│   │       │   ├── SalekhPos.Support.Api/
│   │       │   └── SalekhPos.Support.Contracts/
│   │       │
│   │       └── SystemAdministration/
│   │           ├── SalekhPos.SystemAdministration.Domain/
│   │           │   ├── RootAuthority/
│   │           │   ├── SuperAdmins/
│   │           │   ├── PlatformSettings/
│   │           │   ├── TenantAdministration/
│   │           │   ├── SystemActions/
│   │           │   ├── Incidents/
│   │           │   └── Events/
│   │           ├── SalekhPos.SystemAdministration.Application/
│   │           │   ├── RegisterSuperAdmin/
│   │           │   ├── RevokeSuperAdmin/
│   │           │   ├── ManageTenant/
│   │           │   ├── ManagePlatform/
│   │           │   ├── ManageFeatures/
│   │           │   └── Diagnostics/
│   │           ├── SalekhPos.SystemAdministration.Infrastructure/
│   │           ├── SalekhPos.SystemAdministration.Api/
│   │           └── SalekhPos.SystemAdministration.Contracts/
│   │
│   └── tests/
│       ├── Architecture/
│       ├── Unit/
│       │   ├── Identity/
│       │   ├── Tenancy/
│       │   ├── Organizations/
│       │   ├── Stores/
│       │   ├── Authorization/
│       │   ├── Employees/
│       │   ├── Devices/
│       │   ├── Catalog/
│       │   ├── Pricing/
│       │   ├── Promotions/
│       │   ├── Inventory/
│       │   ├── Warehousing/
│       │   ├── Suppliers/
│       │   ├── Purchasing/
│       │   ├── Sales/
│       │   ├── Payments/
│       │   ├── Returns/
│       │   ├── CashManagement/
│       │   ├── ShiftManagement/
│       │   ├── Customers/
│       │   ├── Loyalty/
│       │   ├── Accounting/
│       │   ├── Taxation/
│       │   ├── Fiscalization/
│       │   ├── Reporting/
│       │   ├── Analytics/
│       │   ├── Notifications/
│       │   ├── Audit/
│       │   ├── Billing/
│       │   ├── Subscriptions/
│       │   ├── Integrations/
│       │   ├── Sync/
│       │   └── SystemAdministration/
│       │
│       ├── Integration/
│       ├── Contract/
│       └── Functional/
│
├── packages/
│   ├── api-client/
│   │   ├── src/
│   │   ├── generated/
│   │   ├── tests/
│   │   └── package.json
│   │
│   ├── contracts/
│   │   ├── api/
│   │   ├── events/
│   │   ├── sync/
│   │   ├── schemas/
│   │   ├── openapi/
│   │   └── generated/
│   │
│   ├── ui/
│   │   ├── components/
│   │   ├── tokens/
│   │   ├── themes/
│   │   ├── typography/
│   │   ├── icons/
│   │   └── accessibility/
│   │
│   ├── localization/
│   │   ├── locales/
│   │   │   ├── en/
│   │   │   ├── az/
│   │   │   ├── ka/
│   │   │   ├── tr/
│   │   │   ├── ru/
│   │   │   └── other/
│   │   ├── currencies/
│   │   ├── date-formats/
│   │   ├── number-formats/
│   │   ├── timezone-data/
│   │   └── country-config/
│   │
│   ├── validation/
│   ├── logging/
│   ├── telemetry/
│   ├── security/
│   ├── permissions/
│   ├── feature-flags/
│   ├── testing/
│   └── config/
│
├── database/
│   ├── schemas/
│   │   ├── identity/
│   │   ├── tenancy/
│   │   ├── organizations/
│   │   ├── stores/
│   │   ├── authorization/
│   │   ├── employees/
│   │   ├── devices/
│   │   ├── catalog/
│   │   ├── pricing/
│   │   ├── promotions/
│   │   ├── inventory/
│   │   ├── warehousing/
│   │   ├── suppliers/
│   │   ├── purchasing/
│   │   ├── sales/
│   │   ├── payments/
│   │   ├── returns/
│   │   ├── cash_management/
│   │   ├── shift_management/
│   │   ├── customers/
│   │   ├── loyalty/
│   │   ├── accounting/
│   │   ├── taxation/
│   │   ├── fiscalization/
│   │   ├── reporting/
│   │   ├── analytics/
│   │   ├── notifications/
│   │   ├── audit/
│   │   ├── billing/
│   │   ├── subscriptions/
│   │   ├── integrations/
│   │   ├── sync/
│   │   └── system_administration/
│   │
│   ├── migrations/
│   ├── seed/
│   │   ├── reference-data/
│   │   ├── development/
│   │   └── testing/
│   ├── views/
│   ├── materialized-views/
│   ├── functions/
│   ├── indexes/
│   ├── policies/
│   ├── partitions/
│   ├── scripts/
│   │   ├── setup/
│   │   ├── migration/
│   │   ├── maintenance/
│   │   ├── diagnostics/
│   │   ├── backup/
│   │   └── restore/
│   └── README.md
│
├── sync/
│   ├── protocol/
│   │   ├── messages/
│   │   ├── commands/
│   │   ├── events/
│   │   ├── contracts/
│   │   ├── schemas/
│   │   └── versioning/
│   │
│   ├── client/
│   │   ├── outbox/
│   │   ├── inbox/
│   │   ├── queue/
│   │   ├── upload/
│   │   ├── download/
│   │   ├── retries/
│   │   ├── checkpoints/
│   │   ├── deduplication/
│   │   ├── conflict-detection/
│   │   ├── conflict-resolution/
│   │   └── diagnostics/
│   │
│   ├── server/
│   │   ├── ingestion/
│   │   ├── validation/
│   │   ├── deduplication/
│   │   ├── reconciliation/
│   │   ├── replication/
│   │   ├── checkpoints/
│   │   ├── conflicts/
│   │   └── diagnostics/
│   │
│   └── tests/
│       ├── protocol/
│       ├── offline/
│       ├── conflicts/
│       ├── retries/
│       └── compatibility/
│
├── integrations/
│   ├── payments/
│   │   ├── abstraction/
│   │   ├── paddle/
│   │   ├── stripe/
│   │   ├── adyen/
│   │   ├── local-providers/
│   │   └── tests/
│   │
│   ├── accounting/
│   │   ├── abstraction/
│   │   ├── quickbooks/
│   │   ├── xero/
│   │   ├── custom/
│   │   └── tests/
│   │
│   ├── ecommerce/
│   │   ├── abstraction/
│   │   ├── shopify/
│   │   ├── woocommerce/
│   │   ├── custom/
│   │   └── tests/
│   │
│   ├── fiscalization/
│   │   ├── abstraction/
│   │   ├── countries/
│   │   │   ├── GE/
│   │   │   ├── AZ/
│   │   │   ├── TR/
│   │   │   ├── US/
│   │   │   └── others/
│   │   └── tests/
│   │
│   ├── hardware/
│   │   ├── printers/
│   │   ├── barcode-scanners/
│   │   ├── cash-drawers/
│   │   ├── scales/
│   │   ├── card-terminals/
│   │   └── customer-displays/
│   │
│   ├── communications/
│   │   ├── email/
│   │   ├── sms/
│   │   └── push/
│   │
│   └── webhooks/
│       ├── incoming/
│       │   ├── validation/
│       │   ├── signatures/
│       │   ├── replay-protection/
│       │   └── handlers/
│       └── outgoing/
│           ├── endpoints/
│           ├── signatures/
│           ├── deliveries/
│           ├── retries/
│           └── logs/
│
├── country-packs/
│   ├── core/
│   │   ├── CountryDefinition/
│   │   ├── TaxDefinition/
│   │   ├── FiscalizationDefinition/
│   │   ├── ReceiptDefinition/
│   │   ├── InvoiceDefinition/
│   │   ├── ComplianceDefinition/
│   │   └── ValidationDefinition/
│   │
│   ├── GE/
│   │   ├── config/
│   │   ├── taxes/
│   │   ├── fiscalization/
│   │   ├── receipts/
│   │   ├── invoices/
│   │   ├── compliance/
│   │   ├── validation/
│   │   ├── integrations/
│   │   └── translations/
│   │
│   ├── AZ/
│   │   ├── config/
│   │   ├── taxes/
│   │   ├── fiscalization/
│   │   ├── receipts/
│   │   ├── invoices/
│   │   ├── compliance/
│   │   ├── validation/
│   │   ├── integrations/
│   │   └── translations/
│   │
│   ├── TR/
│   ├── US/
│   ├── GB/
│   ├── DE/
│   └── other-countries/
│
├── services/
│   ├── background-worker/
│   │   ├── scheduled/
│   │   ├── recurring/
│   │   ├── maintenance/
│   │   └── recovery/
│   │
│   ├── notification-worker/
│   ├── report-worker/
│   ├── analytics-worker/
│   ├── sync-worker/
│   ├── integration-worker/
│   ├── import-export-worker/
│   ├── audit-worker/
│   └── cleanup-worker/
│
├── infra/
│   ├── docker/
│   │   ├── api.Dockerfile
│   │   ├── worker.Dockerfile
│   │   ├── web.Dockerfile
│   │   ├── migration.Dockerfile
│   │   ├── nginx/
│   │   └── local/
│   │
│   ├── compose/
│   │   ├── docker-compose.yml
│   │   ├── docker-compose.dev.yml
│   │   ├── docker-compose.test.yml
│   │   ├── docker-compose.staging.yml
│   │   └── docker-compose.observability.yml
│   │
│   ├── terraform/
│   │   ├── modules/
│   │   │   ├── network/
│   │   │   ├── compute/
│   │   │   ├── load-balancer/
│   │   │   ├── database/
│   │   │   ├── replicas/
│   │   │   ├── cache/
│   │   │   ├── messaging/
│   │   │   ├── object-storage/
│   │   │   ├── cdn/
│   │   │   ├── dns/
│   │   │   ├── certificates/
│   │   │   ├── secrets/
│   │   │   ├── observability/
│   │   │   ├── security/
│   │   │   ├── backups/
│   │   │   └── disaster-recovery/
│   │   │
│   │   └── environments/
│   │       ├── development/
│   │       ├── staging/
│   │       └── production/
│   │
│   ├── kubernetes/
│   │   ├── base/
│   │   │   ├── api/
│   │   │   ├── workers/
│   │   │   ├── web/
│   │   │   ├── ingress/
│   │   │   ├── config/
│   │   │   ├── secrets/
│   │   │   ├── autoscaling/
│   │   │   └── network-policies/
│   │   └── overlays/
│   │       ├── development/
│   │       ├── staging/
│   │       └── production/
│   │
│   ├── nginx/
│   │   ├── nginx.conf
│   │   ├── security-headers.conf
│   │   ├── rate-limits.conf
│   │   └── upstreams/
│   │
│   ├── database/
│   ├── redis/
│   ├── messaging/
│   ├── storage/
│   ├── networking/
│   ├── load-balancing/
│   └── security/
│       ├── waf/
│       ├── firewall/
│       ├── network-policies/
│       ├── hardening/
│       └── secrets/
│
├── observability/
│   ├── opentelemetry/
│   ├── prometheus/
│   │   ├── config/
│   │   └── rules/
│   ├── grafana/
│   │   ├── dashboards/
│   │   └── provisioning/
│   ├── loki/
│   ├── tempo/
│   ├── alerts/
│   │   ├── api/
│   │   ├── database/
│   │   ├── cache/
│   │   ├── workers/
│   │   ├── sync/
│   │   ├── integrations/
│   │   └── security/
│   ├── dashboards/
│   │   ├── platform/
│   │   ├── tenants/
│   │   ├── stores/
│   │   ├── api/
│   │   ├── database/
│   │   ├── queues/
│   │   ├── sync/
│   │   └── business/
│   ├── logs/
│   ├── traces/
│   ├── metrics/
│   └── slo/
│       ├── availability.yml
│       ├── api-latency.yml
│       ├── error-rate.yml
│       ├── sync-latency.yml
│       ├── background-jobs.yml
│       └── database.yml
│
├── security/
│   ├── threat-model/
│   │   ├── assets.md
│   │   ├── trust-boundaries.md
│   │   ├── attack-surfaces.md
│   │   ├── data-flows.md
│   │   └── STRIDE.md
│   │
│   ├── authentication/
│   │   ├── password-policy.md
│   │   ├── mfa.md
│   │   ├── passkeys.md
│   │   ├── sessions.md
│   │   └── recovery.md
│   │
│   ├── authorization/
│   │   ├── roles.md
│   │   ├── permissions.md
│   │   ├── scopes.md
│   │   ├── policies.md
│   │   └── super-admin.md
│   │
│   ├── tenancy/
│   │   └── isolation-model.md
│   │
│   ├── encryption/
│   │   ├── data-at-rest.md
│   │   ├── data-in-transit.md
│   │   └── key-management.md
│   │
│   ├── secrets/
│   │   └── README.md
│   │
│   ├── incident-response/
│   │   ├── incident-plan.md
│   │   ├── security-breach.md
│   │   └── credential-compromise.md
│   │
│   ├── vulnerability-management/
│   ├── dependency-security/
│   ├── secure-coding/
│   ├── privacy/
│   └── compliance/
│
├── tests/
│   ├── unit/
│   │   ├── backend/
│   │   ├── web/
│   │   ├── desktop/
│   │   └── mobile/
│   │
│   ├── integration/
│   │   ├── database/
│   │   ├── cache/
│   │   ├── messaging/
│   │   ├── object-storage/
│   │   ├── authentication/
│   │   ├── payments/
│   │   └── external-services/
│   │
│   ├── contract/
│   │   ├── api/
│   │   ├── events/
│   │   ├── sync/
│   │   └── integrations/
│   │
│   ├── e2e/
│   │   ├── registration/
│   │   ├── owner/
│   │   ├── manager/
│   │   ├── cashier/
│   │   ├── inventory-clerk/
│   │   ├── warehouse-clerk/
│   │   ├── accountant/
│   │   ├── customer-workflows/
│   │   └── super-admin/
│   │
│   ├── security/
│   │   ├── authentication/
│   │   ├── authorization/
│   │   ├── tenant-isolation/
│   │   ├── IDOR/
│   │   ├── injection/
│   │   ├── XSS/
│   │   ├── CSRF/
│   │   ├── SSRF/
│   │   ├── rate-limiting/
│   │   ├── secrets/
│   │   └── abuse/
│   │
│   ├── performance/
│   │   ├── load/
│   │   ├── stress/
│   │   ├── spike/
│   │   ├── soak/
│   │   ├── concurrency/
│   │   └── scalability/
│   │
│   ├── resilience/
│   │   ├── database-failure/
│   │   ├── cache-failure/
│   │   ├── broker-failure/
│   │   ├── network-partition/
│   │   ├── service-restart/
│   │   ├── provider-timeout/
│   │   └── partial-outage/
│   │
│   ├── chaos/
│   ├── migration/
│   ├── compatibility/
│   ├── offline/
│   ├── sync/
│   ├── fixtures/
│   └── test-data/
│
├── tools/
│   ├── cli/
│   │   ├── SalekhPos.Cli/
│   │   └── commands/
│   │       ├── database/
│   │       ├── tenant/
│   │       ├── users/
│   │       ├── diagnostics/
│   │       ├── sync/
│   │       └── maintenance/
│   │
│   ├── database/
│   ├── migration/
│   ├── codegen/
│   ├── openapi/
│   ├── localization/
│   ├── seed/
│   ├── diagnostics/
│   ├── load-testing/
│   ├── security/
│   └── scripts/
│       ├── powershell/
│       └── bash/
│
├── docs/
│   ├── architecture/
│   │   ├── overview.md
│   │   ├── system-context.md
│   │   ├── containers.md
│   │   ├── components.md
│   │   ├── module-boundaries.md
│   │   ├── domain-model.md
│   │   ├── data-flow.md
│   │   ├── tenancy.md
│   │   ├── organizations.md
│   │   ├── stores.md
│   │   ├── authorization.md
│   │   ├── root-super-admin.md
│   │   ├── offline-first.md
│   │   ├── synchronization.md
│   │   ├── hardware.md
│   │   ├── events.md
│   │   ├── api-versioning.md
│   │   ├── database.md
│   │   ├── caching.md
│   │   ├── messaging.md
│   │   ├── security.md
│   │   ├── observability.md
│   │   ├── scalability.md
│   │   ├── resilience.md
│   │   └── disaster-recovery.md
│   │
│   ├── adr/
│   │   ├── 0001-monorepo.md
│   │   ├── 0002-modular-monolith.md
│   │   ├── 0003-postgresql.md
│   │   ├── 0004-multi-tenancy.md
│   │   ├── 0005-module-owned-data.md
│   │   ├── 0006-event-driven-communication.md
│   │   ├── 0007-transactional-outbox.md
│   │   ├── 0008-offline-first-pos.md
│   │   ├── 0009-api-versioning.md
│   │   └── 0010-country-packs.md
│   │
│   ├── api/
│   ├── database/
│   ├── development/
│   │   ├── setup-windows.md
│   │   ├── setup-linux.md
│   │   ├── setup-macos.md
│   │   ├── backend.md
│   │   ├── web.md
│   │   ├── desktop.md
│   │   ├── mobile.md
│   │   └── docker.md
│   │
│   ├── deployment/
│   │   ├── development.md
│   │   ├── staging.md
│   │   ├── production.md
│   │   ├── rollback.md
│   │   └── migrations.md
│   │
│   ├── operations/
│   ├── security/
│   ├── compliance/
│   ├── testing/
│   ├── localization/
│   ├── countries/
│   ├── integrations/
│   ├── offline/
│   ├── sync/
│   ├── hardware/
│   ├── troubleshooting/
│   └── runbooks/
│       ├── api-outage.md
│       ├── database-outage.md
│       ├── cache-outage.md
│       ├── message-broker-outage.md
│       ├── sync-failure.md
│       ├── payment-provider-outage.md
│       ├── fiscal-provider-outage.md
│       ├── security-incident.md
│       ├── compromised-account.md
│       ├── rollback.md
│       └── restore-from-backup.md
│
├── configs/
│   ├── development/
│   │   ├── backend/
│   │   ├── web/
│   │   ├── desktop/
│   │   ├── mobile/
│   │   └── infrastructure/
│   ├── testing/
│   ├── staging/
│   └── production/
│
├── localization/
│   ├── languages/
│   │   ├── en/
│   │   ├── az/
│   │   ├── ka/
│   │   ├── tr/
│   │   ├── ru/
│   │   └── others/
│   ├── countries/
│   ├── currencies/
│   ├── timezones/
│   ├── date-formats/
│   ├── number-formats/
│   ├── tax-regimes/
│   └── locale-formats/
│
├── deploy/
│   ├── development/
│   ├── staging/
│   ├── production/
│   ├── rollback/
│   └── migrations/
│
├── scripts/
│   ├── setup.ps1
│   ├── setup.sh
│   ├── dev-start.ps1
│   ├── dev-stop.ps1
│   ├── build-all.ps1
│   ├── lint-all.ps1
│   ├── test-all.ps1
│   ├── test-unit.ps1
│   ├── test-integration.ps1
│   ├── test-e2e.ps1
│   ├── database-start.ps1
│   ├── database-reset.ps1
│   ├── database-migrate.ps1
│   ├── database-backup.ps1
│   ├── database-restore.ps1
│   ├── observability-start.ps1
│   └── generate-api-client.ps1
│
├── certificates/
│   ├── development/
│   └── README.md
│
├── samples/
│   ├── api/
│   │   ├── authentication/
│   │   ├── sales/
│   │   ├── products/
│   │   └── inventory/
│   ├── webhooks/
│   ├── integrations/
│   ├── hardware/
│   └── country-packs/
│
├── artifacts/
│   └── .gitkeep
│
├── .dockerignore
├── .editorconfig
├── .env.example
├── .gitattributes
├── .gitignore
├── .npmrc
├── .nvmrc
├── Directory.Build.props
├── Directory.Build.targets
├── Directory.Packages.props
├── global.json
├── package.json
├── pnpm-lock.yaml
├── pnpm-workspace.yaml
├── turbo.json
├── docker-compose.yml
├── SalekhPos.sln
├── LICENSE
├── SECURITY.md
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
├── CHANGELOG.md
├── ARCHITECTURE.md
└── README.md
```