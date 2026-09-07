-- Membership and effective permission grants. Provisioning is intentionally not
-- granted to the API runtime; an audited administration workflow is still required.
BEGIN;
CREATE SCHEMA access;
REVOKE ALL ON SCHEMA access FROM PUBLIC;

CREATE TABLE access.memberships (
    organization_id uuid NOT NULL REFERENCES organization.organizations,
    membership_id uuid NOT NULL CHECK (membership_id <> '00000000-0000-0000-0000-000000000000'),
    issuer text NOT NULL CHECK (char_length(issuer) BETWEEN 1 AND 2048),
    subject text NOT NULL CHECK (char_length(subject) BETWEEN 1 AND 256),
    is_active boolean NOT NULL DEFAULT true,
    valid_from timestamptz NOT NULL DEFAULT now(),
    valid_until timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, membership_id),
    UNIQUE (organization_id, issuer, subject),
    CHECK (valid_until IS NULL OR valid_until > valid_from)
);

CREATE TABLE access.permission_grants (
    organization_id uuid NOT NULL,
    grant_id uuid NOT NULL CHECK (grant_id <> '00000000-0000-0000-0000-000000000000'),
    membership_id uuid NOT NULL,
    permission text NOT NULL CHECK (permission ~ '^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$' AND char_length(permission) <= 128),
    scope_kind text NOT NULL CHECK (scope_kind IN ('organization', 'business', 'region', 'branch')),
    business_id uuid,
    region_id uuid,
    branch_id uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, grant_id),
    FOREIGN KEY (organization_id, membership_id) REFERENCES access.memberships,
    FOREIGN KEY (organization_id, business_id) REFERENCES organization.businesses,
    FOREIGN KEY (organization_id, business_id, region_id) REFERENCES organization.regions,
    FOREIGN KEY (organization_id, business_id, branch_id)
        REFERENCES organization.branches (organization_id, business_id, branch_id),
    CHECK (
        (scope_kind = 'organization' AND business_id IS NULL AND region_id IS NULL AND branch_id IS NULL)
        OR (scope_kind = 'business' AND business_id IS NOT NULL AND region_id IS NULL AND branch_id IS NULL)
        OR (scope_kind = 'region' AND business_id IS NOT NULL AND region_id IS NOT NULL AND branch_id IS NULL)
        OR (scope_kind = 'branch' AND business_id IS NOT NULL AND region_id IS NULL AND branch_id IS NOT NULL)
    ),
    UNIQUE NULLS NOT DISTINCT (organization_id, membership_id, permission, scope_kind, business_id, region_id, branch_id)
);

ALTER TABLE access.memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE access.memberships FORCE ROW LEVEL SECURITY;
ALTER TABLE access.permission_grants ENABLE ROW LEVEL SECURITY;
ALTER TABLE access.permission_grants FORCE ROW LEVEL SECURITY;

CREATE POLICY own_membership ON access.memberships
    USING (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid
        AND issuer = current_setting('app.issuer', true)
        AND subject = current_setting('app.subject', true));
CREATE POLICY own_grants ON access.permission_grants
    USING (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid
        AND EXISTS (SELECT FROM access.memberships m
            WHERE m.organization_id = permission_grants.organization_id
              AND m.membership_id = permission_grants.membership_id
              AND m.is_active AND m.valid_from <= statement_timestamp()
              AND (m.valid_until IS NULL OR m.valid_until > statement_timestamp())));

GRANT USAGE ON SCHEMA access TO salekhpos_runtime;
GRANT SELECT ON access.memberships, access.permission_grants TO salekhpos_runtime;
COMMIT;
