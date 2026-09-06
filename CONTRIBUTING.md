# Contributing

Read `docs/development/working-agreement.md`, the master implementation plan,
and the requirements coverage matrix before changing behavior. Keep changes small
and reviewable. Record the implemented behavior, evidence, and remaining gaps;
never describe a mock or an untested provider/device integration as ready.

Install PowerShell 7.2 or later, the exact .NET SDK from `global.json`, and Docker
with Linux container support. Run the same gate used by GitHub Actions from the
repository root:

```powershell
pwsh -NoProfile -File scripts/ci/run.ps1
```

On the configured Windows development machine, the locally installed PostgreSQL
18 binaries can be used instead of Docker:

```powershell
pwsh -NoProfile -File scripts/ci/run.ps1 -PostgresMode Native
```

The native runner expects the binaries documented by `scripts/test-postgres.ps1`.
Both modes require network access for package advisories and the verified scanner
download. Build artifacts, disposable PostgreSQL data, and scanner/test reports
are written outside the OneDrive source tree. Docker test data is deleted when
the gate finishes. Never point these tests at a shared or production database.

The gate checks project boundaries and solution coverage, secrets, locked restore,
Release build with warnings as errors, formatting/analyzers, direct and transitive
dependency advisories, ordered SQL migrations and regression tests, and the whole
.NET test suite against a disposable PostgreSQL database. Missing DB configuration
must fail integration tests. The runner generates runtime/admin connections through
`SALEKHPOS_TEST_RUNTIME_CONNECTION` and `SALEKHPOS_TEST_ADMIN_CONNECTION`; their
credentials are local to that test run and are masked in GitHub logs.

SQL migrations use `NNN_description.sql`. Add one or more regression files with
the same `NNN_` prefix under `infra/postgres/tests`. CI applies each migration and
its tests before moving to the next version, preserving checks for older schemas.
Include tenant isolation, minimum runtime privileges, invalid input, and rollback
or retry scenarios relevant to the change. Do not edit an already deployed
migration; add the next migration and document deployment/rollback implications.

SharedKernel has no external package or application dependency. A module may
reference SharedKernel and projects within its own module; a cross-module contract
requires an explicit architecture decision and corresponding guard update. Hosts
compose modules. Production projects must never reference tests. Add every new
project to `SalekhPos.slnx` and commit its `packages.lock.json`. For a deliberate
dependency update, restore to update lock files, review the dependency diff and
advisories, then run the locked gate. Never bypass a failing security gate by
silently suppressing its warnings.

The GitHub workflow runs on pushes, pull requests, manual dispatch, and a weekly
advisory check. The repository owner should require **Build, security and PostgreSQL**
before merging once the remote repository and branch rules are configured. A
workflow file by itself does not enable branch protection or prove a hosted run.
Dependabot covers NuGet packages and pinned Actions; new project directories must
be added to its configuration. Review the PostgreSQL image digest and Gitleaks
version/checksums when updating tools. These pins were verified against upstream
release metadata; do not replace them with floating `latest` tags.

Upstream references: [GitHub workflow security](https://docs.github.com/en/actions/reference/security/secure-use),
[setup-dotnet](https://github.com/actions/setup-dotnet),
[dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format),
[NuGet dependency listing](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list),
[Gitleaks](https://github.com/gitleaks/gitleaks).
