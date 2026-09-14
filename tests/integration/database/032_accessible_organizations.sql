\set ON_ERROR_STOP on
BEGIN;
INSERT INTO organization.organizations(organization_id,name)
VALUES ('32000000-0000-0000-0000-000000000001','Visible organization'),
       ('32000000-0000-0000-0000-000000000002','Expired organization');
INSERT INTO access.memberships(organization_id,membership_id,issuer,subject,valid_from,valid_until)
VALUES ('32000000-0000-0000-0000-000000000001',gen_random_uuid(),'https://identity.example','org-reader',now()-interval '1 day',NULL),
       ('32000000-0000-0000-0000-000000000002',gen_random_uuid(),'https://identity.example','org-reader',now()-interval '2 days',now()-interval '1 day');

DO $$ BEGIN
    IF EXISTS (
        SELECT FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace,
        LATERAL aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) acl
        WHERE n.nspname = 'access' AND p.proname = 'list_accessible_organizations'
          AND acl.grantee = 0 AND acl.privilege_type = 'EXECUTE') THEN
        RAISE EXCEPTION 'Public can execute organization discovery';
    END IF;
END $$;

SET LOCAL ROLE salekhpos_runtime;
DO $$
DECLARE fn_oid oid;
BEGIN
    IF current_user <> 'salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime identity mismatch'; END IF;
    IF EXISTS (SELECT FROM pg_auth_members WHERE member=(SELECT oid FROM pg_roles WHERE rolname=current_user)) THEN
        RAISE EXCEPTION 'Runtime unexpectedly inherits roles';
    END IF;
    IF (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE ((n.nspname='organization' AND c.relname IN ('organizations','businesses','regions','branches'))
            OR (n.nspname='access' AND c.relname IN ('memberships','permission_grants')))
          AND c.relrowsecurity AND c.relforcerowsecurity
          AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user)) <> 6 THEN
        RAISE EXCEPTION 'Runtime RLS safety set mismatch';
    END IF;
    SELECT p.oid INTO fn_oid FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
      WHERE n.nspname='access' AND p.proname='list_accessible_organizations'
        AND pg_get_function_identity_arguments(p.oid)='p_issuer text, p_subject text, p_after uuid, p_limit integer';
    IF fn_oid IS NULL THEN RAISE EXCEPTION 'Discovery function signature mismatch'; END IF;
    IF NOT EXISTS (SELECT FROM pg_proc p JOIN pg_roles r ON r.oid=p.proowner WHERE p.oid=fn_oid
        AND p.prosecdef AND r.rolname='salekhpos_access_reader'
        AND NOT r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls) THEN
        RAISE EXCEPTION 'Discovery function owner safety mismatch';
    END IF;
    IF NOT has_function_privilege(current_user,fn_oid,'EXECUTE') THEN RAISE EXCEPTION 'Runtime lacks discovery execute'; END IF;
    IF EXISTS (SELECT FROM aclexplode(COALESCE((SELECT proacl FROM pg_proc WHERE oid=fn_oid),acldefault('f',(SELECT proowner FROM pg_proc WHERE oid=fn_oid)))) a WHERE a.grantee=0 AND a.privilege_type='EXECUTE') THEN
        RAISE EXCEPTION 'Public execute remained on discovery function';
    END IF;
END $$;
DO $$ BEGIN
    IF EXISTS (SELECT FROM organization.organizations) OR EXISTS (SELECT FROM access.memberships) THEN
        RAISE EXCEPTION 'Organization discovery weakened direct runtime RLS';
    END IF;
    IF (SELECT count(*) FROM access.list_accessible_organizations(
            'https://identity.example','org-reader',NULL,101)) <> 1 THEN
        RAISE EXCEPTION 'Active membership organization discovery failed';
    END IF;
    IF (SELECT organization_id FROM access.list_accessible_organizations(
            'https://identity.example','org-reader',NULL,101))
        <> '32000000-0000-0000-0000-000000000001'::uuid THEN
        RAISE EXCEPTION 'Expired membership was exposed';
    END IF;
    BEGIN
        PERFORM * FROM access.list_accessible_organizations(
            'https://identity.example','org-reader',NULL,102);
        RAISE EXCEPTION 'Unbounded organization query was accepted';
    EXCEPTION WHEN invalid_parameter_value THEN NULL;
    END;
END $$;
ROLLBACK;
\echo Accessible organization RLS and bounds checks passed.
