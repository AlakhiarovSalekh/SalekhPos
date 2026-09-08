# Web development

Install the Node version in `.nvmrc` and `pnpm@11.19.0`.
Run `pnpm install --frozen-lockfile --ignore-scripts` from the root.
`pnpm --filter @salekhpos/web dev` starts the web client; the backend must run in
a separate terminal with `scripts/dev-start.ps1 -App Backend`.

`pnpm --filter @salekhpos/web build` creates the production build.
`pnpm --filter @salekhpos/web lint` and `typecheck` verify the TypeScript sources.
Real-provider browser tests use `scripts/test-e2e.ps1`; they do not silently skip
when local Java/Keycloak/PostgreSQL prerequisites are missing.

See `docs/architecture/web-authentication.md` for provider configuration and
remaining identity capabilities. An unconfigured workspace displays an explicit
sign-in-unavailable state, not a simulated logged-in user.
