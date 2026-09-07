# Development setup on Linux

Install PowerShell 7, the SDK pinned in `global.json`, and Docker.
Run `pwsh -File scripts/setup.ps1` and `pwsh -File scripts/build-all.ps1`.
Run `pwsh -File scripts/test-all.ps1 -PostgresMode Docker` for the full gate.
The Docker runner binds its disposable PostgreSQL database exclusively to loopback
and removes its own container on completion. It never targets a production database.
Start the API with `pwsh -File scripts/dev-start.ps1`; stop it with Ctrl+C.
