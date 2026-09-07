-- Platform authority is distinct from all tenant memberships and role claims.
BEGIN;
DO $$ BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='salekhpos_systemadministration') THEN
        CREATE ROLE salekhpos_systemadministration NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='salekhpos_bootstrap') THEN
        CREATE ROLE salekhpos_bootstrap NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;
    IF EXISTS (SELECT FROM pg_roles WHERE rolname IN ('salekhpos_systemadministration','salekhpos_bootstrap')
        AND (rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN
        RAISE EXCEPTION 'Unsafe platform database roles';
    END IF;
END $$;
CREATE SCHEMA system_administration AUTHORIZATION salekhpos_systemadministration;
REVOKE ALL ON SCHEMA system_administration FROM PUBLIC;
SET LOCAL ROLE salekhpos_systemadministration;
CREATE TABLE system_administration.super_admins (
    admin_id uuid PRIMARY KEY CHECK (admin_id <> '00000000-0000-0000-0000-000000000000'),
    issuer text NOT NULL CHECK (char_length(issuer) BETWEEN 1 AND 2048),
    subject text NOT NULL CHECK (char_length(subject) BETWEEN 1 AND 256),
    is_root boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
    revoked_at timestamptz,
    CHECK (NOT is_root OR revoked_at IS NULL),
    CHECK (revoked_at IS NULL OR revoked_at >= created_at)
);
CREATE UNIQUE INDEX one_original_root ON system_administration.super_admins (is_root) WHERE is_root;
CREATE UNIQUE INDEX one_active_identity ON system_administration.super_admins (issuer,subject) WHERE revoked_at IS NULL;
CREATE TABLE system_administration.authority_audit (
    operation_id uuid PRIMARY KEY CHECK (operation_id <> '00000000-0000-0000-0000-000000000000'),
    action text NOT NULL CHECK (action IN ('root.bootstrapped','super_admin.registered','super_admin.revoked')),
    actor_issuer text NOT NULL,
    actor_subject text NOT NULL,
    target_key text NOT NULL,
    target_id uuid NOT NULL REFERENCES system_administration.super_admins,
    reason text NOT NULL CHECK (char_length(reason) BETWEEN 1 AND 1000),
    trace_id text NOT NULL CHECK (trace_id ~ '^[a-fA-F0-9]{32}$'),
    recorded_at timestamptz NOT NULL DEFAULT statement_timestamp(),
    database_actor name NOT NULL DEFAULT session_user,
    authenticated_at timestamptz,
    result jsonb NOT NULL
);
ALTER TABLE system_administration.super_admins ENABLE ROW LEVEL SECURITY;
ALTER TABLE system_administration.super_admins FORCE ROW LEVEL SECURITY;
ALTER TABLE system_administration.authority_audit ENABLE ROW LEVEL SECURITY;
ALTER TABLE system_administration.authority_audit FORCE ROW LEVEL SECURITY;
-- Only the non-login function owner can access rows. Runtime receives EXECUTE,
-- never table privileges or membership in the owner/bootstrap roles.
CREATE POLICY function_owner ON system_administration.super_admins TO salekhpos_systemadministration USING (true) WITH CHECK (true);
CREATE POLICY function_owner ON system_administration.authority_audit TO salekhpos_systemadministration USING (true) WITH CHECK (true);

CREATE FUNCTION system_administration.protect_admin() RETURNS trigger
LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $$ BEGIN
    IF TG_OP='DELETE' OR OLD.is_root THEN
        RAISE EXCEPTION 'Authority history and original root are immutable' USING ERRCODE='42501';
    END IF;
    IF ROW(NEW.admin_id,NEW.issuer,NEW.subject,NEW.is_root,NEW.created_at)
        IS DISTINCT FROM ROW(OLD.admin_id,OLD.issuer,OLD.subject,OLD.is_root,OLD.created_at)
        OR OLD.revoked_at IS NOT NULL OR NEW.revoked_at IS NULL THEN
        RAISE EXCEPTION 'Only one-way administrator revocation is allowed' USING ERRCODE='42501';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER protect_admin BEFORE UPDATE OR DELETE ON system_administration.super_admins
    FOR EACH ROW EXECUTE FUNCTION system_administration.protect_admin();
CREATE FUNCTION system_administration.deny_history_mutation() RETURNS trigger
LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $$ BEGIN
    RAISE EXCEPTION 'Authority history is immutable' USING ERRCODE='42501';
END $$;
CREATE TRIGGER immutable_audit BEFORE UPDATE OR DELETE ON system_administration.authority_audit
    FOR EACH ROW EXECUTE FUNCTION system_administration.deny_history_mutation();
CREATE TRIGGER no_audit_truncate BEFORE TRUNCATE ON system_administration.authority_audit
    FOR EACH STATEMENT EXECUTE FUNCTION system_administration.deny_history_mutation();
CREATE TRIGGER no_admin_truncate BEFORE TRUNCATE ON system_administration.super_admins
    FOR EACH STATEMENT EXECUTE FUNCTION system_administration.deny_history_mutation();

CREATE FUNCTION system_administration.admin_response(a system_administration.super_admins) RETURNS jsonb
LANGUAGE sql STABLE SET search_path=pg_catalog,pg_temp SET timezone='UTC' AS $$
    SELECT jsonb_build_object('id',a.admin_id,'issuer',a.issuer,'subject',a.subject,
        'isRoot',a.is_root,'isActive',a.revoked_at IS NULL,'createdAt',a.created_at,'revokedAt',a.revoked_at)
$$;
CREATE FUNCTION system_administration.require_root(p_issuer text,p_subject text,p_mfa boolean,p_auth timestamptz) RETURNS void
LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $$ BEGIN
    IF p_mfa IS DISTINCT FROM true OR p_auth IS NULL
        OR p_auth < clock_timestamp()-interval '5 minutes' OR p_auth > clock_timestamp()+interval '30 seconds'
        OR NOT EXISTS(SELECT FROM system_administration.super_admins
            WHERE issuer=p_issuer AND subject=p_subject AND is_root AND revoked_at IS NULL) THEN
        RAISE EXCEPTION 'Root authority with recent MFA required' USING ERRCODE='42501';
    END IF;
END $$;
CREATE FUNCTION system_administration.validate_operation(p_operation uuid,p_reason text,p_trace text) RETURNS void
LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $$ BEGIN
    IF p_operation IS NULL OR p_operation='00000000-0000-0000-0000-000000000000'
        OR p_reason IS NULL OR char_length(p_reason) NOT BETWEEN 1 AND 1000 OR btrim(p_reason)=''
        OR p_trace IS NULL OR p_trace !~ '^[a-fA-F0-9]{32}$' THEN
        RAISE EXCEPTION 'Invalid operation' USING ERRCODE='22023';
    END IF;
END $$;
CREATE FUNCTION system_administration.replay(p_operation uuid,p_action text,p_issuer text,p_subject text,p_target text,p_reason text) RETURNS jsonb
LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $$
DECLARE a system_administration.authority_audit;
BEGIN
    SELECT * INTO a FROM system_administration.authority_audit WHERE operation_id=p_operation;
    IF NOT FOUND THEN RETURN NULL; END IF;
    IF ROW(a.action,a.actor_issuer,a.actor_subject,a.target_key,a.reason)
        IS DISTINCT FROM ROW(p_action,p_issuer,p_subject,p_target,p_reason) THEN
        RAISE EXCEPTION 'Operation identifier reused with different input' USING ERRCODE='P0001';
    END IF;
    RETURN a.result;
END $$;
CREATE FUNCTION system_administration.bootstrap_root(p_operation uuid,p_issuer text,p_subject text,p_reason text,p_trace text) RETURNS jsonb
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE a system_administration.super_admins; r jsonb;
BEGIN
    PERFORM system_administration.validate_operation(p_operation,p_reason,p_trace);
    IF p_issuer IS NULL OR char_length(p_issuer) NOT BETWEEN 1 AND 2048 OR p_issuer NOT LIKE 'https://%'
        OR p_subject IS NULL OR char_length(p_subject) NOT BETWEEN 1 AND 256 OR btrim(p_subject)='' THEN
        RAISE EXCEPTION 'Invalid root identity' USING ERRCODE='22023';
    END IF;
    PERFORM pg_advisory_xact_lock(72531,1);
    r := system_administration.replay(p_operation,'root.bootstrapped',p_issuer,p_subject,p_subject,p_reason);
    IF r IS NOT NULL THEN RETURN r; END IF;
    IF EXISTS(SELECT FROM system_administration.super_admins WHERE is_root) THEN
        RAISE EXCEPTION 'Original root already established' USING ERRCODE='P0001';
    END IF;
    INSERT INTO system_administration.super_admins(admin_id,issuer,subject,is_root)
        VALUES(gen_random_uuid(),p_issuer,p_subject,true) RETURNING * INTO a;
    r := system_administration.admin_response(a);
    INSERT INTO system_administration.authority_audit(operation_id,action,actor_issuer,actor_subject,target_key,target_id,reason,trace_id,result)
        VALUES(p_operation,'root.bootstrapped',p_issuer,p_subject,p_subject,a.admin_id,p_reason,p_trace,r);
    RETURN r;
END $$;
CREATE FUNCTION system_administration.authority(p_issuer text,p_subject text) RETURNS jsonb
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
    SELECT jsonb_build_object('isRoot',COALESCE(bool_or(is_root),false),'isSuperAdmin',count(*)>0)
    FROM system_administration.super_admins WHERE issuer=p_issuer AND subject=p_subject AND revoked_at IS NULL
$$;
CREATE FUNCTION system_administration.register_super_admin(p_operation uuid,p_issuer text,p_actor text,p_mfa boolean,p_auth timestamptz,p_subject text,p_reason text,p_trace text) RETURNS jsonb
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE a system_administration.super_admins; r jsonb;
BEGIN
    PERFORM system_administration.require_root(p_issuer,p_actor,p_mfa,p_auth);
    PERFORM system_administration.validate_operation(p_operation,p_reason,p_trace);
    IF p_subject IS NULL OR char_length(p_subject) NOT BETWEEN 1 AND 256 OR btrim(p_subject)='' THEN
        RAISE EXCEPTION 'Invalid target identity' USING ERRCODE='22023';
    END IF;
    PERFORM pg_advisory_xact_lock(72531,1);
    r := system_administration.replay(p_operation,'super_admin.registered',p_issuer,p_actor,p_subject,p_reason);
    PERFORM system_administration.require_root(p_issuer,p_actor,p_mfa,p_auth);
    IF r IS NOT NULL THEN RETURN r; END IF;
    IF EXISTS(SELECT FROM system_administration.super_admins WHERE issuer=p_issuer AND subject=p_subject AND revoked_at IS NULL) THEN
        RAISE EXCEPTION 'Identity already has active authority' USING ERRCODE='P0001';
    END IF;
    INSERT INTO system_administration.super_admins(admin_id,issuer,subject)
        VALUES(gen_random_uuid(),p_issuer,p_subject) RETURNING * INTO a;
    r := system_administration.admin_response(a);
    INSERT INTO system_administration.authority_audit(operation_id,action,actor_issuer,actor_subject,target_key,target_id,reason,trace_id,authenticated_at,result)
        VALUES(p_operation,'super_admin.registered',p_issuer,p_actor,p_subject,a.admin_id,p_reason,p_trace,p_auth,r);
    RETURN r;
END $$;
CREATE FUNCTION system_administration.revoke_super_admin(p_operation uuid,p_issuer text,p_actor text,p_mfa boolean,p_auth timestamptz,p_target uuid,p_reason text,p_trace text) RETURNS jsonb
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE a system_administration.super_admins; r jsonb;
BEGIN
    PERFORM system_administration.require_root(p_issuer,p_actor,p_mfa,p_auth);
    PERFORM system_administration.validate_operation(p_operation,p_reason,p_trace);
    IF p_target IS NULL OR p_target='00000000-0000-0000-0000-000000000000' THEN
        RAISE EXCEPTION 'Invalid target identity' USING ERRCODE='22023';
    END IF;
    PERFORM pg_advisory_xact_lock(72531,1);
    r := system_administration.replay(p_operation,'super_admin.revoked',p_issuer,p_actor,p_target::text,p_reason);
    PERFORM system_administration.require_root(p_issuer,p_actor,p_mfa,p_auth);
    IF r IS NOT NULL THEN RETURN r; END IF;
    SELECT * INTO a FROM system_administration.super_admins WHERE admin_id=p_target FOR UPDATE;
    IF NOT FOUND OR a.is_root OR a.revoked_at IS NOT NULL THEN
        RAISE EXCEPTION 'Target cannot be revoked' USING ERRCODE='P0001';
    END IF;
    UPDATE system_administration.super_admins SET revoked_at=clock_timestamp() WHERE admin_id=p_target RETURNING * INTO a;
    r := system_administration.admin_response(a);
    INSERT INTO system_administration.authority_audit(operation_id,action,actor_issuer,actor_subject,target_key,target_id,reason,trace_id,authenticated_at,result)
        VALUES(p_operation,'super_admin.revoked',p_issuer,p_actor,p_target::text,a.admin_id,p_reason,p_trace,p_auth,r);
    RETURN r;
END $$;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA system_administration FROM PUBLIC;
GRANT USAGE ON SCHEMA system_administration TO salekhpos_runtime,salekhpos_bootstrap;
GRANT EXECUTE ON FUNCTION system_administration.bootstrap_root(uuid,text,text,text,text) TO salekhpos_bootstrap;
GRANT EXECUTE ON FUNCTION system_administration.authority(text,text),
    system_administration.register_super_admin(uuid,text,text,boolean,timestamptz,text,text,text),
    system_administration.revoke_super_admin(uuid,text,text,boolean,timestamptz,uuid,text,text) TO salekhpos_runtime;
RESET ROLE;
COMMIT;
