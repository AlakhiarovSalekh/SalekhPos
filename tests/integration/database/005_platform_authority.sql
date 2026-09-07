BEGIN;
SET LOCAL ROLE salekhpos_runtime;
DO $$ BEGIN
    IF system_administration.authority('https://id.test','owner') <> '{"isRoot":false,"isSuperAdmin":false}'::jsonb THEN
        RAISE EXCEPTION 'Unexpected initial platform authority';
    END IF;
    BEGIN
        PERFORM system_administration.bootstrap_root(gen_random_uuid(),'https://id.test','owner','Unauthorized bootstrap',repeat('a',32));
        RAISE EXCEPTION 'Runtime bootstrap allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        INSERT INTO system_administration.super_admins(admin_id,issuer,subject,is_root)
            VALUES(gen_random_uuid(),'https://id.test','attacker',true);
        RAISE EXCEPTION 'Runtime table mutation allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        PERFORM * FROM system_administration.authority_audit;
        RAISE EXCEPTION 'Runtime audit enumeration allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
RESET ROLE;
SET LOCAL ROLE salekhpos_bootstrap;
SELECT system_administration.bootstrap_root('50000000-0000-0000-0000-000000000001','https://id.test','owner','Establish original owner',repeat('a',32));
SELECT system_administration.bootstrap_root('50000000-0000-0000-0000-000000000001','https://id.test','owner','Establish original owner',repeat('b',32));
DO $$ BEGIN
    BEGIN
        PERFORM system_administration.bootstrap_root(gen_random_uuid(),'https://id.test','replacement','Replace original root',repeat('a',32));
        RAISE EXCEPTION 'Second root allowed' USING ERRCODE='XX000';
    EXCEPTION WHEN raise_exception THEN NULL; END;
END $$;
RESET ROLE;
SET LOCAL ROLE salekhpos_runtime;
DO $$ BEGIN
    BEGIN
        PERFORM system_administration.register_super_admin(gen_random_uuid(),'https://id.test','tenant-owner',true,now(),'attacker','Escalate tenant owner',repeat('a',32));
        RAISE EXCEPTION 'Tenant escalation allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        PERFORM system_administration.register_super_admin(gen_random_uuid(),'https://id.test','owner',false,now(),'attacker','No MFA',repeat('a',32));
        RAISE EXCEPTION 'Missing MFA allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        PERFORM system_administration.register_super_admin(gen_random_uuid(),'https://id.test','owner',true,now()-interval '6 minutes','attacker','Stale MFA',repeat('a',32));
        RAISE EXCEPTION 'Old MFA allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
SELECT system_administration.register_super_admin('50000000-0000-0000-0000-000000000002','https://id.test','owner',true,now(),'support','Authorized support',repeat('a',32));
SELECT system_administration.register_super_admin('50000000-0000-0000-0000-000000000002','https://id.test','owner',true,now(),'support','Authorized support',repeat('b',32));
DO $$ BEGIN
    BEGIN
        PERFORM system_administration.register_super_admin(gen_random_uuid(),'https://id.test','support',true,now(),'attacker','Delegate authority',repeat('a',32));
        RAISE EXCEPTION 'Additional admin created an admin';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        PERFORM system_administration.register_super_admin('50000000-0000-0000-0000-000000000002','https://id.test','owner',true,now(),'different','Authorized support',repeat('a',32));
        RAISE EXCEPTION 'Conflicting replay allowed' USING ERRCODE='XX000';
    EXCEPTION WHEN raise_exception THEN NULL; END;
END $$;
RESET ROLE;
DO $$ BEGIN
    IF (SELECT count(*) FROM system_administration.super_admins)<>2
        OR (SELECT count(*) FROM system_administration.authority_audit)<>2 THEN RAISE EXCEPTION 'Duplicate authority or audit'; END IF;
END $$;
SET LOCAL ROLE salekhpos_systemadministration;
DO $$ BEGIN
    BEGIN
        UPDATE system_administration.super_admins SET subject='changed' WHERE is_root;
        RAISE EXCEPTION 'Root identity changed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        DELETE FROM system_administration.super_admins WHERE is_root;
        RAISE EXCEPTION 'Root deleted';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        UPDATE system_administration.authority_audit SET reason='changed';
        RAISE EXCEPTION 'Audit changed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        TRUNCATE system_administration.authority_audit;
        RAISE EXCEPTION 'Audit truncated';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
RESET ROLE;
-- Inject audit-storage failure after registry insertion; both must roll back.
CREATE FUNCTION pg_temp.reject_test_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
    IF NEW.target_key='audit-failure' THEN RAISE EXCEPTION 'Injected audit failure'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER test_audit_failure BEFORE INSERT ON system_administration.authority_audit
    FOR EACH ROW EXECUTE FUNCTION pg_temp.reject_test_audit();
SET LOCAL ROLE salekhpos_runtime;
DO $$ BEGIN
    BEGIN
        PERFORM system_administration.register_super_admin(gen_random_uuid(),'https://id.test','owner',true,now(),'audit-failure','Audit rollback test',repeat('a',32));
        RAISE EXCEPTION 'Audit failure did not propagate' USING ERRCODE='XX000';
    EXCEPTION WHEN raise_exception THEN NULL; END;
END $$;
RESET ROLE;
DO $$ BEGIN
    IF EXISTS(SELECT FROM system_administration.super_admins WHERE subject='audit-failure') THEN
        RAISE EXCEPTION 'Unaudited administrator survived rollback';
    END IF;
END $$;
ROLLBACK;
