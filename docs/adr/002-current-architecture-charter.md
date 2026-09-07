# ADR 002: Adopt the current architecture charter

Status: Accepted, September 7, 2026. Supersedes ADR 001's repository-layout decision.

The user supplied the 168-section Master Architecture Prompt and explicitly asked
to implement the system using it. Its full, byte-identical source is preserved at
`docs/requirements/master-architecture-charter.md`. SHA256:
`B9CF83F9DACAFACAB861C7554901FE27451041602290F9D385ECB88B778FA8CB`.

Move the small existing backend into `backend/src/Bootstrapper/SalekhPos.Api`,
`backend/src/BuildingBlocks/SalekhPos.SharedKernel`, `backend/src/Modules` and
`backend/tests`. Preserve public namespaces, API routes, data and existing SQL
migrations. Keep the modern `SalekhPos.sln` solution format: a `.sln` conversion
would add no functionality. Update all executable paths and verify the full suite.

The alternative was retaining `server/` indefinitely. Aligning now is inexpensive
and makes subsequent module work follow the explicit user-requested target map.
This is a relocation of working code, not a rewrite or empty scaffold.

Existing Organizations and Access assemblies remain incremental architecture debt:
Access reads organization data to evaluate branch scope. Do not replicate this
cross-module query pattern in new modules. Extract owned read contracts when
evolving these workflows. New Identity data is owned exclusively by Identity.
Split substantive module layers into separate assemblies as functionality grows;
do not create unused projects to simulate architectural completeness.

The charter describes native .NET desktop structure. Previous Flutter desktop
planning is superseded; the actual desktop framework requires an offline/hardware
proof before selection. Web and mobile clients do not exist yet. No application
directories are created before their implementation.

Organization remains the existing tenant identifier; Branch remains the existing
store identifier. Renaming persisted tables or `/api/v1/organizations/.../branches`
would break compatibility without improving tenant isolation.

Current slice: durable revocation of the current API access token, with an atomic
immutable audit record and fail-closed verification on every authenticated request.
This does not revoke an OIDC provider session or refresh token. Those require a
provider-specific integration and remain explicit next steps.
