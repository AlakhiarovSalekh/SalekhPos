# SalekhPos

A retail operating platform being developed for real stores.

**Current status:** a verified backend foundation, not a production-ready POS.
Organization/business/region/branch models, PostgreSQL RLS, JWT-protected branch
reads, active membership, permission scopes and durable current-token revocation
with an immutable audit record are implemented. Protected original-root bootstrap
and root-only Super Admin registration/revocation are implemented; provider login
and MFA enrollment, tenant onboarding, sales, inventory, offline operation and
hardware integrations remain open.

## Technical foundation

- .NET SDK 10.0.400, ASP.NET Core/.NET 10.
- PostgreSQL 18; separate web, desktop and mobile target applications under the
  [current architecture charter](docs/requirements/master-architecture-charter.md).
- Immutable Money values with explicit currency, exact decimal addition/subtraction,
  overflow detection and protection against silent precision loss.
- xUnit domain/HTTP tests and real PostgreSQL integration tests.
- Locked package versions and hashes; strict compilation and static analysis.

## Verification on Windows

From the repository root, with PowerShell 7:

~~~powershell
./scripts/dotnet.ps1 restore SalekhPos.slnx --configfile NuGet.Config --locked-mode
./scripts/ci/run.ps1 -PostgresMode Native
./scripts/dotnet.ps1 run --project backend/src/Bootstrapper/SalekhPos.Api --no-restore --configuration Release -- --urls http://127.0.0.1:5080
~~~

The .NET wrapper first uses the development SDK under the system temporary
salekhpos-dotnet directory, then the system dotnet command. global.json pins
the exact SDK; reinstall that version if temporary tools have been removed.
[Microsoft installation guidance](https://learn.microsoft.com/dotnet/core/install/windows).

The full test runner creates a disposable PostgreSQL instance, applies migrations
and SQL regressions, restores a logical backup into another database, and runs all
.NET tests against the restored database. Direct solution-wide dotnet test fails
without integration database configuration rather than silently skipping tests.
For domain/HTTP tests alone, select backend/tests/SalekhPos.Tests.
See [CONTRIBUTING.md](CONTRIBUTING.md) for Docker and hosted CI.

## Implemented endpoints

- GET /health/live: process liveness.
- GET /health/ready: 503 when unconfigured; 200 with suitable identity configuration
  and a restricted RLS-enabled database means authorized branch reads are ready,
  not that sales or the entire product are ready.
- GET /api/v1/organizations/{id}/branches: JWT, active membership and scoped
  branches.view; keyset pagination with a maximum page size of 100.
- GET /api/v1/organizations/{id}/branches/{branchId}: the same authorization checks.
- POST /api/v1/identity/revoke-current-token: revoke the validated current API
  credential durably; subsequent authenticated requests using it return 401.
- GET /api/v1/platform/authority: current persisted platform authority.
- POST /api/v1/platform/super-admins and /{adminId}/revoke: original-root-only,
  recent-MFA-protected, audited, idempotent administrator registration/revocation.

Local HTTP is for development. Provider selection, production TLS/proxy settings,
session/device revocation, auditing and operating controls remain release gates.
See [access configuration and limitations](docs/architecture/access-foundation.md).
Token revocation does not revoke provider sessions or refresh tokens; see
[the exact revocation contract and deployment constraints](docs/architecture/token-revocation.md).
See [platform administration and operator bootstrap](docs/architecture/platform-administration.md)
for the separate root authority model. No production owner identity is seeded.

Build artifacts, live databases, credentials and test reports stay outside the
OneDrive source directory. Independent checkouts on the same machine must use
different ArtifactsPath values to avoid sharing build output.

## Project documentation

- [Current architectural source of truth](docs/requirements/master-architecture-charter.md)
- [All 168 current charter sections](docs/architecture/current-charter-index.md)
- [Current adoption decisions](docs/adr/002-current-architecture-charter.md)
- [Earlier requirement coverage](docs/architecture/master-specification-coverage.md)
- [Earlier phased implementation plan](docs/architecture/master-implementation-plan.md)
- [Implemented API contract](docs/api/openapi.json)
- [Architecture decisions and original design](docs/architecture/technology-and-architecture.md)
- [Accepted repository structure](docs/architecture/repository-structure.md)
- [Acceptance tests and delivery plan](docs/architecture/acceptance-and-delivery-plan.md)
- [Implementation progress](docs/development/progress.md)
- [Working agreement](docs/development/working-agreement.md)
- [Organization and branch boundaries](docs/architecture/organization-foundation.md)

All project documentation, code comments and commit messages use English.
User conversation may be Azerbaijani. Multilingual test fixtures remain intentional
localization test data.
