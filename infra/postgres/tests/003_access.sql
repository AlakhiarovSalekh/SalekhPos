\set ON_ERROR_STOP on
BEGIN;
INSERT INTO organization.organizations(organization_id,name)
VALUES ('30000000-0000-0000-0000-000000000001','Access A'),('30000000-0000-0000-0000-000000000002','Access B');
INSERT INTO access.memberships(organization_id,membership_id,issuer,subject)
VALUES ('30000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000011','https://identity.example','alice'),
       ('30000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000012','https://identity.example','bob');
INSERT INTO access.permission_grants(organization_id,grant_id,membership_id,permission,scope_kind)
VALUES ('30000000-0000-0000-0000-000000000001',gen_random_uuid(),'30000000-0000-0000-0000-000000000011','branches.view','organization');
SET LOCAL ROLE salekhpos_runtime;
DO $$ BEGIN
    IF EXISTS(SELECT FROM access.memberships) OR EXISTS(SELECT FROM access.permission_grants) THEN
        RAISE EXCEPTION 'Missing identity exposed memberships';
    END IF;
END $$;
SELECT set_config('app.organization_id','30000000-0000-0000-0000-000000000001',true),
       set_config('app.issuer','https://identity.example',true),set_config('app.subject','alice',true);
DO $$ BEGIN
    IF (SELECT count(*) FROM access.memberships) <> 1 OR (SELECT count(*) FROM access.permission_grants) <> 1 THEN
        RAISE EXCEPTION 'Own membership and grants unavailable';
    END IF;
    BEGIN
        UPDATE access.memberships SET is_active=true;
        RAISE EXCEPTION 'Runtime modified membership';
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    BEGIN
        INSERT INTO access.permission_grants(organization_id,grant_id,membership_id,permission,scope_kind)
        VALUES ('30000000-0000-0000-0000-000000000001',gen_random_uuid(),'30000000-0000-0000-0000-000000000011','security.roles.manage','organization');
        RAISE EXCEPTION 'Runtime escalated permissions';
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
END $$;
SELECT set_config('app.organization_id','30000000-0000-0000-0000-000000000002',true);
DO $$ BEGIN
    IF EXISTS(SELECT FROM access.memberships) OR EXISTS(SELECT FROM access.permission_grants) THEN
        RAISE EXCEPTION 'Tenant selection alone granted membership';
    END IF;
END $$;
ROLLBACK;
\echo Access identity isolation and escalation checks passed.
