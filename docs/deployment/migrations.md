# Database migrations

Inventory migrations with `scripts/ci/get-migrations.ps1`. Versions are globally
ordered and duplicate versions are rejected. Never rewrite an applied migration.
Run migration regressions with `scripts/test-integration.ps1` against disposable
PostgreSQL before release. Production migration orchestration is not implemented;
the reserved production scripts fail explicitly and must not be treated as a
supported deployment procedure.
