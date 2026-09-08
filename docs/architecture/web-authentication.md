# Web authentication

The web application uses Next.js App Router with a same-origin `/auth/*` proxy
to ASP.NET Core. The backend uses the maintained OpenID Connect handler with a
confidential authorization-code client, PKCE, state and nonce validation.
Browser JavaScript never receives provider tokens. An HttpOnly cookie contains
an opaque session reference; PostgreSQL holds the encrypted authentication ticket.

Session rows use forced RLS scoped by a SHA-256 session-key fingerprint. Session
creation, renewal and deletion append an audit event. Logout deletes the server
ticket; renewal deliberately cannot recreate a deleted ticket. Session expiry is
checked by the database. Session APIs never confer tenant or platform permissions;
existing v1 endpoints continue to require validated bearer authentication.

Login and logout require a matching public Origin and an ASP.NET antiforgery
form token. Redirect destinations are configured constants, not user input.
Cookies are Secure outside the explicit Development-only loopback HTTP option.
Production requires an absolute shared data-protection key-ring directory and an
RSA certificate/private key for encryption. Real production key provisioning,
rotation and multi-instance operational tests remain deployment work.

## Configuration

Set `WebAuthentication__Authority`, `WebAuthentication__ClientId`,
`WebAuthentication__ClientSecret`, `WebAuthentication__PublicOrigin` and
`ConnectionStrings__Application` through environment variables or a secret store.
Register exact `/auth/callback` and `/auth/signed-out` URLs at the provider.
For production, also configure `WebAuthentication__KeyRingDirectory`,
`WebAuthentication__KeyCertificatePath` and `WebAuthentication__KeyCertificatePassword`.
The Next server uses `SALEKHPOS_BACKEND_ORIGIN` to select the backend origin.
Never configure a wildcard provider redirect URI.

## Verification and limits

The native browser runner uses Keycloak 26.7.3 and Temurin JDK 25.0.4.1+1 from
user-temporary directories. Keycloak ZIP SHA256 was checked against its release
asset digest: `27a6535553c3cdcd083872ba40629efafb3475e3b758e0c6f691395561dd0f1f`.
It generates an isolated development realm, random client/user credentials and
loopback-only processes, then cleans up its processes and plaintext import file.
Use `scripts/test-postgres.ps1 -RunWebIdentityTests` after building the web app.
This runner is Windows-only; hosted web CI builds/lints/audits separately.

MFA enrollment/step-up, provider back-channel logout, refresh-token rotation,
expired-session cleanup, delegated business API calls and provider-independent
account recovery are not implemented in this slice. The account page is not a
sales dashboard. Refer to `docs/development/progress.md` for the exact test outcome.

References:
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0
- https://nextjs.org/docs/app/guides/authentication
- https://www.keycloak.org/getting-started/getting-started-zip
