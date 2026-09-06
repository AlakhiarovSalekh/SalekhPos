# SalekhPos

A retail operating platform being developed for real stores.

**Current status:** a verified backend foundation, not a production-ready POS.
Organization/business/region/branch models, PostgreSQL RLS, JWT-protected branch
reads, active membership and permission scopes are implemented. Login UI, Owner/MFA
administration, sales, inventory, offline operation and hardware integrations remain open.

## Technical foundation

- .NET SDK 10.0.400, ASP.NET Core/.NET 10.
- PostgreSQL 18; React/TypeScript planned for web, Flutter for native clients.
- Immutable Money values with explicit currency, exact decimal addition/subtraction,
  overflow detection and protection against silent precision loss.
- xUnit domain/HTTP tests and real PostgreSQL integration tests.
- Locked package versions and hashes; strict compilation and static analysis.

## Verification on Windows

From the repository root, with PowerShell 7:

~~~powershell
./scripts/dotnet.ps1 restore SalekhPos.slnx --configfile NuGet.Config --locked-mode
./scripts/ci/run.ps1 -PostgresMode Native
./scripts/dotnet.ps1 run --project server/src/Api --no-restore --configuration Release -- --urls http://127.0.0.1:5080
~~~

The .NET wrapper first uses the development SDK under the system temporary
salekhpos-dotnet directory, then the system dotnet command. global.json pins
the exact SDK; reinstall that version if temporary tools have been removed.
[Microsoft installation guidance](https://learn.microsoft.com/dotnet/core/install/windows).

The full test runner creates a disposable PostgreSQL instance, applies migrations
and SQL regressions, restores a logical backup into another database, and runs all
.NET tests against the restored database. Direct solution-wide dotnet test fails
without integration database configuration rather than silently skipping tests.
For domain/HTTP tests alone, select server/tests/SalekhPos.Tests.
See [CONTRIBUTING.md](CONTRIBUTING.md) for Docker and hosted CI.

## Implemented endpoints

- GET /health/live: process liveness.
- GET /health/ready: 503 when unconfigured; 200 with suitable identity configuration
  and a restricted RLS-enabled database means authorized branch reads are ready,
  not that sales or the entire product are ready.
- GET /api/v1/organizations/{id}/branches: JWT, active membership and scoped
  branches.view; keyset pagination with a maximum page size of 100.
- GET /api/v1/organizations/{id}/branches/{branchId}: the same authorization checks.

Local HTTP is for development. Provider selection, production TLS/proxy settings,
session/device revocation, auditing and operating controls remain release gates.
See [access configuration and limitations](docs/architecture/access-foundation.md).

Build artifacts, live databases, credentials and test reports stay outside the
OneDrive source directory. Independent checkouts on the same machine must use
different ArtifactsPath values to avoid sharing build output.

## Project documentation

- [Complete requirement coverage](docs/architecture/master-specification-coverage.md)
- [Current architecture and phased implementation plan](docs/architecture/master-implementation-plan.md)
- [Implemented API contract](docs/api/openapi.json)
- [Architecture decisions and original design](docs/architecture/technology-and-architecture.md)
- [Accepted repository structure](docs/architecture/repository-structure.md)
- [Acceptance tests and delivery plan](docs/architecture/acceptance-and-delivery-plan.md)
- [Implementation progress](docs/development/progress.md)
- [Working agreement](docs/development/working-agreement.md)
- [Organization and branch boundaries](docs/architecture/organization-foundation.md)

All project communication, documentation, code comments and commit messages use
English. Multilingual test fixtures remain intentional localization test data.
