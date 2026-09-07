# Current access-token revocation

POST `/api/v1/identity/revoke-current-token` requires a valid API bearer token and
returns 204 only after durable persistence. It accepts no target identity. A body
cannot select another user's token. No membership or tenant role is needed to
revoke one's own credential; the credential may cover multiple organizations.

Every authenticated API request checks the durable revocation table after real
JWT validation. Revoked tokens receive 401, including retries of the revocation
request. Concurrent requests already authenticated before revocation may finish;
this is not transaction cancellation. Separate credentials remain valid.

The server stores SHA256 of the validated JWT signing input (header.payload), issuer, subject, UTC token
expiry, database-generated revocation time, a stable action and server trace ID.
It never stores the raw token. Hashing avoids requiring a provider-specific sid or
jti claim and preserves the existing token contract. The record itself is the
atomic revocation audit: a successful revocation cannot lack its audit row.
Equivalent signed contents share revocation regardless of signature encoding; an issuer must vary claims such as jti when issuing a distinct credential. Duplicate inserts are harmless. Runtime SQL cannot update, delete, truncate or
backdate the audit. Forced RLS restricts reads and inserts to the current identity;
transaction-local context is cleared before pooled connections are reused.

The Identity module owns its schema. The host supplies a shared pool, without
allowing Identity to depend on Access types or tables. Runtime safety is checked
before reads and writes. Missing storage or unsafe runtime permissions fail closed;
readiness includes revocation storage, while process liveness remains independent.
No positive authentication result is cached across requests or API instances.

This revokes one API access token. It does not terminate the identity provider's
browser session, revoke refresh tokens, disable an account, or invalidate offline
POS authorization. UI wording must say 'Revoke current API token' until provider
logout, refresh-token revocation and device/session workflows are implemented.
There are no supported offline clients yet; offline credential leases require
explicit policies before their implementation.

Deployment: apply migration 004 before deploying this API. Existing JWTs remain
compatible. Rolling back to an API that does not check revocations would re-enable
unexpired revoked tokens and is unsafe; retain the check in rollback builds.
Expired revocation rows are retained for audit in this development slice. A
privileged retention/export policy with tested retention beyond token expiry and
validator clock skew is a production release gate. There is deliberately no
runtime deletion endpoint or speculative retention period.

Verification includes real signed tokens, restart persistence, duplicate writes,
actor isolation, denied mutation, request-body manipulation, fail-closed storage
and pool cleanup, plus all previous tenant and branch security tests.
