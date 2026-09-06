# Repository structure

Status: accepted target structure, September 7, 2026.

The master documents define a clean monorepo and explicitly require the real tree to match the chosen frameworks and repository reality. SalekhPos therefore keeps the proven `server/` foundation instead of renaming it to `backend/`, uses the concise top-level groups from the engineering specification, and expands each group only when implementation begins.

```text
SalekhPos/
├── apps/
│   ├── web/                 # React and TypeScript management application
│   └── client/              # Flutter native client: Windows, macOS, Linux, Android, iOS
├── server/
│   ├── src/
│   │   ├── Api/             # Composition root and HTTP boundary
│   │   ├── Modules/         # Business capabilities, internally layered
│   │   ├── SharedKernel/    # Small stable primitives shared by modules
│   │   └── Workers/         # Background entry points when first required
│   └── tests/               # Unit, integration, architecture, security, contract, load
├── packages/                # Versioned cross-client contracts, localization, design tokens
├── integrations/            # Provider adapters grouped by payment/fiscal/etc. capability
├── infra/
│   ├── postgres/            # Ordered migrations, SQL validation, maintenance and recovery
│   ├── containers/          # Local and CI container definitions when required
│   └── deployment/          # Environment-specific IaC when a provider is selected
├── docs/
│   ├── requirements/        # Immutable supplied master documents
│   ├── adr/                 # Accepted architectural decisions
│   ├── architecture/        # Current system design and traceability
│   ├── api/                 # Machine-readable and explanatory API contracts
│   ├── security/            # Threat model, controls and evidence when introduced
│   └── development/         # Workflow, plans and verified progress
├── scripts/                 # Developer and CI automation
└── .github/                 # Repository governance and CI workflows
```

## Mapping to the master structures

| Master concept | Adopted location | Reason |
|---|---|---|
| `apps/web` | `apps/web` | Exact match for the required React/TypeScript web application |
| `apps/client` | `apps/client` | Exact match for the required five-platform Flutter client |
| `backend` | `server` | The existing .NET solution already uses this clear name; renaming adds churn without improving boundaries |
| backend API and workers | `server/src/Api`, `server/src/Workers` | Separate deployable composition roots |
| backend domain/application/infrastructure | inside each `server/src/Modules/<Capability>` | Capability ownership is clearer than global technical layers and prevents unrelated modules sharing internals |
| backend modules | `server/src/Modules` | Exact semantic match; directories appear as capabilities are implemented |
| database | `infra/postgres` | Keeps database migrations beside infrastructure automation while retaining a dedicated ownership boundary |
| packages | `packages` | Shared artifacts only; server domain code is not moved here |
| integrations | `integrations` plus module-owned ports | Provider SDK isolation without allowing providers to define the domain |
| infrastructure | `infra` | Short existing name, same responsibility |
| tests | beside the owning application, with future system tests at root when needed | Fast ownership and framework-specific tooling; cross-system suites may live in root `tests/` |
| tools | `scripts` | Existing executable automation convention; standalone developer tools may be added under `tools/` if they become products |

## Expansion rules

1. Create a directory when its first maintained artifact is added. Empty scaffolding does not prove architecture.
2. A business module owns its domain, application use cases, persistence adapters, and public contracts. It cannot reference another module's internal types.
3. The API and workers compose modules; they contain no financial, inventory, authorization, or synchronization rules.
4. Provider code implements module-owned ports. Payment, fiscal, tax, identity, and hardware vendors remain replaceable.
5. Shared packages require two real consumers and a stable responsibility. Do not use `shared` as a miscellaneous folder.
6. Tests stay close to their owner until a suite crosses application boundaries. Security and tenant-isolation checks may remain in integration projects while small; split them when ownership or runtime cost warrants it.
7. Generated output, secrets, local databases, IDE state, and environment credentials never belong in the repository.
8. New top-level directories require an ADR update and architecture-check update.

This structure improves the detailed sample by preserving all of its responsibilities while avoiding global domain/application/infrastructure projects that can become tightly coupled. It follows the second master document's explicit instruction to adapt the example to the chosen frameworks and actual repository.
