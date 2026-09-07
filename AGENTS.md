# SalekhPos implementation guidance

Read `docs/requirements/master-architecture-charter.md` and the current
`docs/development/progress.md` before continuing implementation. The 168-section
charter is the current architectural source of truth. Older requirement sources
are retained as historical evidence; explicit newer decisions take precedence.

The user's newer explicit structure instruction supersedes incremental folder
creation: preserve every path in `docs/requirements/final-complete-file-structure.md`.
The manifest and `scripts/ci/check-structure.ps1` enforce this delivery contract.
Git keep markers preserve reserved directories; they do not establish functionality.
Implement complete, verified vertical slices. Do not claim the entire POS is finished. Preserve versioned APIs and migration
history. Keep module ownership, tenant isolation, authorization, audit, offline
compatibility and internationalization explicit in each slice.

All code, project documentation and commits are English. User conversation may
be Azerbaijani. Record verified results, limitations and the exact next step in
`docs/development/progress.md` so a request to continue resumes actual work.

Run `./scripts/ci/run.ps1 -PostgresMode Native` on Windows (PowerShell 7), or use
`-PostgresMode Docker` with Docker available. Commit and publish verified milestones
to the configured GitHub origin under the user's standing authorization.
