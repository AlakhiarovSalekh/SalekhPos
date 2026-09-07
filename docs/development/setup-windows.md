# Development setup on Windows

Install PowerShell 7, the exact SDK in `global.json`, and PostgreSQL 18.
Run `./scripts/setup.ps1`, then `./scripts/build-all.ps1` from the repository root.
`./scripts/dev-start.ps1` starts the API in the current terminal. Stop it with Ctrl+C.
Configure the HTTPS OIDC authority, API audience and database connection through
environment variables. Never commit real credentials. Unconfigured protected
endpoints remain unavailable; no development authentication bypass is supplied.

Run `./scripts/test-all.ps1 -PostgresMode Native` for the full disposable gate.
The native PostgreSQL binary path is configurable on `scripts/test-postgres.ps1`.
Client applications are reserved in the supplied layout and are not runnable yet.
