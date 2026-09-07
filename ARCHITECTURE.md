# SalekhPos architecture

The architectural source is [the master charter](docs/requirements/master-architecture-charter.md).
The newer [final structure](docs/requirements/final-complete-file-structure.md)
controls all repository paths. See [the layout and readiness explanation](docs/architecture/repository-structure.md)
and [verified progress](docs/development/progress.md).

SalekhPos uses a modular .NET/PostgreSQL backend with versioned APIs and separate
web, desktop, mobile and kiosk roots. Organization is the tenant boundary.
Platform root authority is distinct from tenant permissions. Desktop offline
sales, synchronization, hardware and country-specific behavior remain required
product work. The presence of their directories is not an implementation claim.
