BEGIN;
DO $$ BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='salekhpos_identity') THEN
        CREATE ROLE salekhpos_identity NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;
    IF EXISTS (SELECT FROM pg_roles WHERE rolname='salekhpos_identity'
        AND (rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN
        RAISE EXCEPTION 'Unsafe identity owner role';
    END IF;
END $$;
GRANT USAGE,CREATE ON SCHEMA identity TO salekhpos_identity;
SET LOCAL ROLE salekhpos_identity;
CREATE TABLE identity.web_sessions (
    key_hash text PRIMARY KEY CHECK (key_hash ~ '^[A-F0-9]{64}$'),
    protected_ticket bytea NOT NULL CHECK (octet_length(protected_ticket) BETWEEN 1 AND 65536),
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE identity.web_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.web_sessions FORCE ROW LEVEL SECURITY;
CREATE POLICY session_key ON identity.web_sessions TO salekhpos_runtime
    USING (key_hash = NULLIF(current_setting('app.web_session_key',true),''))
    WITH CHECK (key_hash = NULLIF(current_setting('app.web_session_key',true),''));
GRANT SELECT, DELETE ON identity.web_sessions TO salekhpos_runtime;
GRANT INSERT (key_hash,protected_ticket,expires_at) ON identity.web_sessions TO salekhpos_runtime;
GRANT UPDATE (protected_ticket,expires_at) ON identity.web_sessions TO salekhpos_runtime;
-- Session mutation audit contains no token, ticket, cookie or user profile.
CREATE TABLE identity.web_session_audit (
    event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    key_hash text NOT NULL,
    action text NOT NULL CHECK (action IN ('INSERT','UPDATE','DELETE')),
    recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE identity.web_session_audit ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.web_session_audit FORCE ROW LEVEL SECURITY;
REVOKE ALL ON identity.web_session_audit FROM PUBLIC, salekhpos_runtime;
CREATE POLICY audit_writer ON identity.web_session_audit FOR INSERT TO salekhpos_identity WITH CHECK (true);
CREATE FUNCTION identity.audit_web_session() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
BEGIN
    INSERT INTO identity.web_session_audit(key_hash,action)
    VALUES (CASE WHEN TG_OP='DELETE' THEN OLD.key_hash ELSE NEW.key_hash END,TG_OP);
    RETURN CASE WHEN TG_OP='DELETE' THEN OLD ELSE NEW END;
END $$;
REVOKE ALL ON FUNCTION identity.audit_web_session() FROM PUBLIC;
CREATE TRIGGER web_session_audit AFTER INSERT OR UPDATE OR DELETE ON identity.web_sessions
    FOR EACH ROW EXECUTE FUNCTION identity.audit_web_session();
COMMIT;
