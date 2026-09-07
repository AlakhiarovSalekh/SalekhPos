> Historical decision: superseded where indicated by ADR 002 and the current architecture charter.

# ADR 001: master specification alignment

Date: 2026-09-07. Status: accepted for implementation; six-platform delivery remains unverified.

The two supplied master documents extend the original small foundation. Retain
the working .NET modular monolith and server/infra layout. Do not mass-create empty
folders or rewrite working code to mimic a sample tree. Module boundaries are
checked by scripts/ci/check-architecture.ps1; an Access module owns its application
contracts and Npgsql infrastructure with no dependency on other module internals.

Use React/TypeScript for the future management web client and Flutter for the five
native platforms. This supersedes the earlier unimplemented Flutter Web proposal:
the explicit React direction in source A is compatible with source B and no working
client is discarded. Web is online-first until browser durability is independently
proven. Native SQLite/offline/hardware proofs remain separate acceptance gates.

Organization is the tenant boundary, with explicit Business and optional Region
before Branch. Existing branch rows are preserved unconfigured until their real
business/timezone can be supplied; no default country, currency or timezone is
invented. Fiscal/provider/tax rules remain configurable and must be verified for
the selected market. A planning framework is not legal compliance.

OIDC is the identity protocol. API access-token validation uses the maintained
Microsoft JwtBearer handler, currently an explicit at+jwt asymmetric profile.
Persisted active membership and effective scoped grants authorize branch reads;
client role/tenant/permission claims cannot replace them. Full role administration,
Owner bootstrap, MFA, BFF/session/device revocation and audit are still required.

PostgreSQL with Npgsql remains the primary transactional store. Direct bounded
parameterized reads are sufficient for the current query; adding EF Core solely
for this query is unnecessary. Cache, broker, microservices, Kubernetes and sharding
wait for measured needs. Initial outbox/inbox and workers remain in the roadmap.

Source traceability is recorded for every section, with immutable original copies.
GitHub publication follows user authorization: verified logical milestones, no
credentials/generated databases, and no production-readiness claim.

The accepted monorepo layout and its exact mapping to both master documents are
defined in [repository-structure.md](../architecture/repository-structure.md).
