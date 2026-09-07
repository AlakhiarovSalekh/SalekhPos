# Database

Migrations 001-003 retain their original bytes and version identities under
`database/migrations`. New module-owned migrations are discovered below
`backend/src/Modules/*/*.Infrastructure/Persistence/Migrations`.
`scripts/ci/get-migrations.ps1` orders the combined inventory and rejects duplicate
versions. Do not edit applied migrations; add a new migration.

SQL regressions live in `tests/integration/database`. The Windows native and Linux
Docker runners apply each migration with its corresponding regressions, restore
a logical backup into a separate database, and run application integration tests.
This proves the tested logical restore, not production disaster recovery.
