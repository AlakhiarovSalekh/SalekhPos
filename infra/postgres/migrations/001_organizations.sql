-- Run once as the migration owner, in a transaction.
-- The deployment must provision salekhpos_runtime as a non-owner, non-superuser,
-- non-BYPASSRLS role before applying this migration. Never grant it migration rights.
BEGIN;

DO $$ BEGIN
    IF current_user = 'salekhpos_runtime' THEN
        RAISE EXCEPTION 'Runtime role must not run migrations';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'salekhpos_runtime') THEN
        RAISE EXCEPTION 'Provision the restricted runtime role before migrating';
    END IF;
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'salekhpos_runtime'
               AND (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication))
       OR EXISTS (SELECT FROM pg_auth_members m JOIN pg_roles r ON r.oid = m.member
                  WHERE r.rolname = 'salekhpos_runtime') THEN
        RAISE EXCEPTION 'Runtime role must have no elevated attributes or role memberships';
    END IF;
END $$;

CREATE SCHEMA organization;
REVOKE ALL ON SCHEMA organization FROM PUBLIC;

CREATE TABLE organization.organizations (
    organization_id uuid PRIMARY KEY CHECK (organization_id <> '00000000-0000-0000-0000-000000000000'),
    name text NOT NULL CHECK (name = btrim(name) AND char_length(name) BETWEEN 1 AND 200),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE organization.branches (
    organization_id uuid NOT NULL REFERENCES organization.organizations(organization_id),
    branch_id uuid NOT NULL CHECK (branch_id <> '00000000-0000-0000-0000-000000000000'),
    code text NOT NULL CHECK (code ~ '^[A-Z0-9][A-Z0-9_-]{0,31}$'),
    name text NOT NULL CHECK (name = btrim(name) AND char_length(name) BETWEEN 1 AND 200),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, branch_id),
    UNIQUE (organization_id, code)
);

ALTER TABLE organization.organizations ENABLE ROW LEVEL SECURITY;
ALTER TABLE organization.organizations FORCE ROW LEVEL SECURITY;
ALTER TABLE organization.branches ENABLE ROW LEVEL SECURITY;
ALTER TABLE organization.branches FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON organization.organizations
    USING (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid)
    WITH CHECK (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid);

CREATE POLICY tenant_isolation ON organization.branches
    USING (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid)
    WITH CHECK (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid);

GRANT USAGE ON SCHEMA organization TO salekhpos_runtime;
GRANT SELECT ON organization.organizations, organization.branches TO salekhpos_runtime;
GRANT UPDATE (name) ON organization.organizations TO salekhpos_runtime;
GRANT INSERT (organization_id, branch_id, code, name),
      UPDATE (code, name, is_active) ON organization.branches TO salekhpos_runtime;

COMMIT;
