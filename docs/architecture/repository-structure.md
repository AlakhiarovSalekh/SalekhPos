# Repository structure

Current decision: [ADR 002](../adr/002-current-architecture-charter.md).
The current charter supersedes the earlier server/client layout decision.
Only implemented components are present:

```text
SalekhPos/
├── backend/
│   ├── src/
│   │   ├── Bootstrapper/SalekhPos.Api/
│   │   ├── BuildingBlocks/SalekhPos.SharedKernel/
│   │   └── Modules/
│   │       ├── Organizations/
│   │       ├── Access/
│   │       └── Identity/
│   │           ├── Application/
│   │           ├── Api/
│   │           ├── Infrastructure/
│   │           └── Persistence/Migrations/
│   └── tests/
├── infra/postgres/           # Immutable legacy migrations and SQL regressions
├── scripts/                 # Windows and CI verification
├── docs/                    # Requirements, decisions, contracts and progress
├── .github/                 # Quality workflow and dependency updates
└── SalekhPos.slnx            # Existing modern .NET solution format
```

The full future tree is in the preserved charter. Web, desktop, mobile, kiosk,
workers and shared client packages are created only with real implementations.
Desktop and mobile have separate target roots; the earlier apps/client plan is
superseded. No frontend is currently implemented.

Module migrations stay with their owner. Existing migrations 001-003 retain their
original paths and contents. The common migration inventory orders both legacy
and owned migrations and rejects duplicate versions. Native and Docker runners
execute the same migration set before backup/restore and integration testing.

The small existing modules remain internally layered assemblies. Splitting their
layers into separate assemblies is incremental work, not an excuse to create
unused interfaces. Architecture CI prevents references between module assemblies;
the existing Access-to-Organizations SQL dependency is documented debt in ADR 002.

Organization is the existing tenant boundary; Branch represents a store. Existing
v1 API paths and persisted names remain compatible. Root Super Admin is a separate
platform authority that has not yet been implemented.
