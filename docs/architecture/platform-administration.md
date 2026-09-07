# Platform administration operations

Implemented routes:

- GET /api/v1/platform/authority returns the authenticated identity's persisted
  isRoot/isSuperAdmin flags. It accepts no requested subject and grants no tenant access.
- POST /api/v1/platform/super-admins accepts operationId, subject and reason.
  Only the original root with recent MFA can register another administrator.
  The target issuer is the validated root's issuer. Clients cannot set isRoot.
- POST /api/v1/platform/super-admins/{adminId}/revoke accepts operationId and reason.
  Only root with recent MFA can revoke an additional administrator. Root itself
  cannot be revoked, deleted, renamed or replaced by this workflow.

Mutation responses are 200 with id, issuer, subject, isRoot, isActive, createdAt and
revokedAt. Timestamps use ISO 8601. Missing/invalid/revoked bearer tokens receive
401. Missing root/MFA authority receives 403. Malformed fields receive 400.
Conflicting operation IDs, duplicate active identities and invalid revocation
state receive 409. Capacity limits return 429 and unavailable persistence 503.

operationId is a nonempty client-generated UUID retained across retries. Exact
replays return the original committed response and do not repeat the mutation or
audit. Different input with the same ID is rejected. Reason is required, at most
1,000 UTF-16 code units, without controls or outer whitespace. Subject is an exact
provider identifier, not an email address or display name. No notification/email
is sent by these registry operations.

## Provider assurance contract

Set Authentication__Authority and Authentication__Audience as described in the
access foundation. Set Authentication__PrivilegedAcr only to an exact assurance
value the chosen issuer guarantees means MFA. The API requires exactly one acr
and auth_time claim on the validated at+jwt access token. auth_time refers to the
original authentication, not token refresh time. Age is at most five minutes;
30 seconds of future tolerance handles bounded clock skew. Configure time sync.

Missing/empty PrivilegedAcr denies privileged mutations. Invalid nonempty settings
fail startup. Production provider selection, MFA enrollment, login, refresh-token
revocation and recovery are still pending. Never configure a password-only ACR as
MFA. Signed test-provider tokens verify the API enforcement, not a real provider's
MFA behavior.

## Original owner bootstrap

Apply migration 005 with deployment credentials capable of establishing the two
restricted database roles. Provision a separate deployment login with membership
in salekhpos_bootstrap. Never grant that membership to salekhpos_runtime or allow
login as salekhpos_systemadministration. Remove bootstrap membership from the
operator after successful commissioning according to deployment policy.

The platform owner must verify the intended account in the trusted IdP, enroll MFA
and obtain its exact issuer and subject before commissioning. No production owner
identity has been chosen or created by the development fixture.

Provide SALEKHPOS_BOOTSTRAP_CONNECTION through the operator's secret environment,
using verified TLS. Then run from the repository root (PowerShell 7):

```powershell
./scripts/dotnet.ps1 run --project tools/cli/SalekhPos.Cli --configuration Release -- bootstrap-root <operation-uuid> <https-issuer> <verified-subject> <reason>
```

Replace the angle-bracket arguments with reviewed values and quote the reason.
Credentials are never arguments. The tool returns 0 after commit, 1 on rejected or
unavailable bootstrap, and 2 for invalid command syntax. Keep the operation UUID
and input so a response lost after commit can be retried safely. It cannot reset
an existing root. It records the database operator as well as the bound identity.
A loopback-only transport exception exists for disposable tests when
SALEKHPOS_BOOTSTRAP_LOCAL_DEVELOPMENT=true; production must not enable it.

The API executable never reads the bootstrap credential. The test runner executes
the real CLI against restored disposable PostgreSQL, checking initial bootstrap,
identical retry, replacement rejection and runtime-credential rejection. Test
issuer identities are fixtures, not production defaults.

## Audit and recovery

Every successful registry mutation inserts its immutable audit row in the same
transaction: operation/action, actor identity, target, reason, trace ID, database
operator, authentication time where applicable, recorded time and response snapshot.
Audit-write failure rolls back the registry change. No table mutation privileges
are granted to the API; triggers also reject history changes by the function role.
Denied attempts use safe application failure logs; a durable security-event pipeline
is still pending and is not claimed as implemented audit of every denied attempt.

Migrations 001-004 remain unchanged. The new migration is module-owned and both
Windows/Docker runners apply it before backup/restore. The logical restore uses
pre-existing cluster roles; disaster recovery into a new cluster must recreate
role definitions and grants before restoration. This is not yet a complete
production recovery runbook. Audit retention/export and root IdP account recovery
remain production release gates.
