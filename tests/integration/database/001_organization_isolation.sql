\set ON_ERROR_STOP on
-- Disposable test database only. Fixtures are rolled back.
BEGIN;
INSERT INTO organization.organizations (organization_id, name) VALUES
('11111111-1111-1111-1111-111111111111', 'Organization A'),
('22222222-2222-2222-2222-222222222222', 'Organization B');
INSERT INTO organization.branches (organization_id, branch_id, code, name) VALUES
('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'MAIN', 'Branch A'),
('22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'MAIN', 'Branch B');

SET LOCAL ROLE salekhpos_runtime;
DO $$ BEGIN
    IF EXISTS (SELECT FROM organization.organizations) OR EXISTS (SELECT FROM organization.branches) THEN
        RAISE EXCEPTION 'Missing context exposed tenant data';
    END IF;
END $$;

SELECT set_config('app.organization_id', '11111111-1111-1111-1111-111111111111', true);
DO $$
DECLARE affected integer;
BEGIN
    IF (SELECT count(*) FROM organization.organizations) <> 1
       OR (SELECT count(*) FROM organization.branches) <> 1
       OR (SELECT name FROM organization.branches) IS DISTINCT FROM 'Branch A' THEN
        RAISE EXCEPTION 'Tenant A isolation failed';
    END IF;
    INSERT INTO organization.branches (organization_id, branch_id, code, name)
    VALUES ('11111111-1111-1111-1111-111111111111', gen_random_uuid(), 'SECOND', 'Allowed branch');
    IF (SELECT count(*) FROM organization.branches) <> 2 THEN
        RAISE EXCEPTION 'Valid same-tenant insert failed';
    END IF;
    UPDATE organization.branches SET name = 'Illegal update'
        WHERE organization_id = '22222222-2222-2222-2222-222222222222';
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected <> 0 THEN RAISE EXCEPTION 'Cross-tenant update succeeded'; END IF;
    BEGIN
        INSERT INTO organization.branches (organization_id, branch_id, code, name)
        VALUES ('22222222-2222-2222-2222-222222222222', gen_random_uuid(), 'ATTACK', 'Illegal');
        RAISE EXCEPTION 'Cross-tenant insert succeeded';
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    BEGIN
        INSERT INTO organization.branches (organization_id, branch_id, code, name)
        VALUES ('11111111-1111-1111-1111-111111111111', gen_random_uuid(), 'MAIN', 'Duplicate');
        RAISE EXCEPTION 'Duplicate branch code succeeded';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
    BEGIN
        DELETE FROM organization.branches;
        RAISE EXCEPTION 'Runtime was allowed to delete branches';
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    BEGIN
        ALTER TABLE organization.branches DISABLE ROW LEVEL SECURITY;
        RAISE EXCEPTION 'Runtime was allowed to disable RLS';
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
END $$;

SELECT set_config('app.organization_id', '22222222-2222-2222-2222-222222222222', true);
DO $$ BEGIN
    IF (SELECT name FROM organization.branches) IS DISTINCT FROM 'Branch B' THEN
        RAISE EXCEPTION 'Tenant B context switch failed';
    END IF;
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = current_user AND (rolsuper OR rolbypassrls OR rolcreaterole)) THEN
        RAISE EXCEPTION 'Runtime has elevated privileges';
    END IF;
END $$;
ROLLBACK;

BEGIN;
SET LOCAL ROLE salekhpos_runtime;
DO $$ BEGIN
    IF nullif(current_setting('app.organization_id', true), '') IS NOT NULL THEN
        RAISE EXCEPTION 'Tenant context escaped the previous transaction';
    END IF;
END $$;
ROLLBACK;
BEGIN;
SELECT set_config('app.organization_id', '11111111-1111-1111-1111-111111111111', true);
COMMIT;
DO $$ BEGIN
    IF nullif(current_setting('app.organization_id', true), '') IS NOT NULL THEN
        RAISE EXCEPTION 'Tenant context escaped a committed transaction';
    END IF;
END $$;
\echo Organization isolation SQL checks passed.
