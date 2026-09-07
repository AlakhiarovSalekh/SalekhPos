# Backend development

Open `SalekhPos.sln`. Package versions are managed by `Directory.Packages.props`.
Use `scripts/setup.ps1`, `scripts/build-all.ps1` and `scripts/test-unit.ps1`.
The complete gate is `scripts/test-all.ps1`; it requires disposable PostgreSQL.

Domain and Application assemblies must remain framework-independent. API modules
use application interfaces, with infrastructure supplied by the API composition
root. Never change v1 route contracts or applied migration contents during moves.
See `docs/architecture/repository-structure.md` for module placement and limitations.
