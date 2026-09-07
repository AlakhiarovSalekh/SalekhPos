# Repository structure

The exact path contract is the user's September 7 replacement specification:
[Final complete file structure](../requirements/final-complete-file-structure.md).
It supersedes the charter's earlier incremental directory-creation rule.
The original source is retained byte-for-byte. Every one of its 1,543 entries
(1,340 directories and 203 named files) is represented in the repository.

`repository-structure-manifest.json` is a machine-readable transcription.
`./scripts/ci/check-structure.ps1 -RequireTracked` verifies every path against the
original tree, including file/directory types and survival after a Git clone.
Leaf directories use `.gitkeep` where implementation is still pending.
Project files, dependency locks, implementation code, evidence documents and
these tracking files supplement the supplied tree; they do not replace its paths.

## Implemented code placement

- API composition: `backend/src/Bootstrapper/SalekhPos.Api`.
- Exact monetary arithmetic: `BuildingBlocks/SalekhPos.SharedKernel/Money`.
- Organization hierarchy: `Modules/Organizations/SalekhPos.Organizations.Domain`.
- Tenant permission reads: `Modules/Authorization`, separate Application and
  Infrastructure assemblies. Existing database schema names remain compatible.
- Token revocation: `Modules/Identity`, separate Application, Infrastructure and
  API assemblies. API depends on an application interface, not PostgreSQL.
- Root authority: the five `Modules/SystemAdministration` layer assemblies.
- Root commissioning CLI: `tools/cli/SalekhPos.Cli`.
- Unit/HTTP tests: `backend/tests/Unit`; integration tests: `backend/tests/Integration`.
- Historical SQL migrations 001-003: `database/migrations`; later module-owned
  migrations stay in their Infrastructure/Persistence/Migrations directories.
- SQL regressions: `tests/integration/database`.
- Root solution: `SalekhPos.sln`; .NET versions: `Directory.Packages.props`.
- Automatic quality workflow: `.github/workflows/ci.yml`.

## Readiness

Directory presence is a delivery-layout guarantee, not a feature guarantee.
Reserved client files and hardware abstractions contain no claim of working POS
behavior. Unimplemented operational scripts/workflows fail explicitly instead
of reporting success or deploying incomplete components. Reserved configuration
must be implemented and validated before use. The only production-readiness
claims allowed are those supported by `docs/development/progress.md`.

Branch remains the persisted/v1 store name. Tenant identity, authorization,
immutable audit, root restrictions, token revocation and migration versions are
preserved through the move. The existing authorization query across organization
schema boundaries remains documented debt; renaming it does not eliminate it.
