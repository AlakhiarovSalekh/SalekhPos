BEGIN;
SET LOCAL ROLE salekhpos_runtime;
SELECT set_config('app.web_session_key',repeat('A',64),true);
INSERT INTO identity.web_sessions(key_hash,protected_ticket,expires_at)
VALUES (repeat('A',64),decode('01','hex'),clock_timestamp()+interval '30 minutes');
DO $$ BEGIN
    IF (SELECT count(*) FROM identity.web_sessions) <> 1 THEN RAISE EXCEPTION 'Own session invisible'; END IF;
    BEGIN
        INSERT INTO identity.web_sessions(key_hash,protected_ticket,expires_at)
        VALUES (repeat('B',64),decode('01','hex'),clock_timestamp()+interval '30 minutes');
        RAISE EXCEPTION 'Cross-session insert succeeded';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        DELETE FROM identity.web_session_audit;
        RAISE EXCEPTION 'Session audit deletion succeeded';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
SELECT set_config('app.web_session_key',repeat('B',64),true);
DO $$ BEGIN
    IF EXISTS(SELECT FROM identity.web_sessions) THEN RAISE EXCEPTION 'Cross-session read succeeded'; END IF;
END $$;
DELETE FROM identity.web_sessions;
SELECT set_config('app.web_session_key',repeat('A',64),true);
DELETE FROM identity.web_sessions;
RESET ROLE;
DO $$ BEGIN
    IF (SELECT count(*) FROM identity.web_session_audit WHERE key_hash=repeat('A',64)) <> 2 THEN
        RAISE EXCEPTION 'Session lifecycle audit incomplete';
    END IF;
END $$;
ROLLBACK;
