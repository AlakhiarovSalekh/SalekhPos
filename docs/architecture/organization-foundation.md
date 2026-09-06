# Organization and branch foundation

Status: migrations 001–003 and their isolation regressions pass on PostgreSQL 18.6.
JWT validation, active membership, organization/business/region/branch scopes and
real connection-pool tests are implemented for branch reads. The complete identity
and Platform Owner system is not finished.

Organization is the tenant boundary. Branch identity is (organization_id, branch_id).
References must retain tenant identity through composite foreign keys; a branch ID
alone is insufficient. Branch codes are unique within an organization. The runtime
role cannot physically delete business records.

Migration 002 adds Organization → Business → optional Region → Branch. New branches
require explicit business and IANA timezone values. Existing branches retain their
identifiers, names, codes and timestamps; missing business/timezone assignments are
not invented. Those legacy rows have is_configured=false and are not returned by
the protected read API.

Domain and database name validation use the same explicit Unicode whitespace/control
sets. Existing invalid names are preserved: NOT VALID constraints protect new or
updated rows, while repairing historical records remains a deliberate operator task.
The migration does not silently trim or rewrite user data.

The runtime role must not own tables, be a superuser or have BYPASSRLS. Migration
credentials are separate. Organization/business provisioning requires an audited
onboarding workflow rather than unrestricted tenant CRUD.

Every application operation validates identity, active membership and scope.
Parameterized set_config calls establish organization/issuer/subject context only
for the current transaction. Client-provided target identifiers are not authority.
Membership/grant RLS supplements application authorization; organization RLS alone
does not establish branch-level access.

The runtime role can change its GUC settings. This mechanism is therefore not an
independent authentication defense against SQL injection or a stolen database
credential. Parameterized queries, least privilege and validated server identity
remain mandatory.

Missing context returns no tenant rows and rejects writes; malformed UUID context
fails explicitly. All six tenant/access tables enable and force RLS.

See [access foundation](access-foundation.md) for the implemented contract and
[PostgreSQL RLS documentation](https://www.postgresql.org/docs/18/ddl-rowsecurity.html)
for database semantics.
