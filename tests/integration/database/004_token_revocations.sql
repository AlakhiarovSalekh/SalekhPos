BEGIN;
SET LOCAL ROLE salekhpos_runtime;
SELECT set_config('app.issuer','https://identity.example.test',true), set_config('app.subject','revocation-test',true);
INSERT INTO identity.revoked_tokens(fingerprint,issuer,subject,expires_at,trace_id)
VALUES(decode(repeat('ab',32),'hex'),'https://identity.example.test','revocation-test',now()+interval '1 hour',repeat('a',32));
DO $$ BEGIN
    IF (SELECT count(*) FROM identity.revoked_tokens) <> 1 THEN RAISE EXCEPTION 'Own audit missing'; END IF;
    BEGIN
        UPDATE identity.revoked_tokens SET expires_at=now();
        RAISE EXCEPTION 'Audit update allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        DELETE FROM identity.revoked_tokens;
        RAISE EXCEPTION 'Audit deletion allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        TRUNCATE identity.revoked_tokens;
        RAISE EXCEPTION 'Audit truncate allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        INSERT INTO identity.revoked_tokens(fingerprint,issuer,subject,expires_at,trace_id)
        VALUES(decode(repeat('cd',32),'hex'),'https://identity.example.test','other-user',now(),repeat('b',32));
        RAISE EXCEPTION 'Cross-identity insertion allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        INSERT INTO identity.revoked_tokens(fingerprint,issuer,subject,expires_at,trace_id,revoked_at)
        VALUES(decode(repeat('ef',32),'hex'),'https://identity.example.test','revocation-test',now(),repeat('b',32),'2000-01-01');
        RAISE EXCEPTION 'Audit time override allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
SELECT set_config('app.subject','other-user',true);
DO $$ BEGIN
    IF EXISTS(SELECT FROM identity.revoked_tokens) THEN RAISE EXCEPTION 'Cross-subject audit leak'; END IF;
END $$;
SELECT set_config('app.subject','revocation-test',true), set_config('app.issuer','https://other.example.test',true);
DO $$ BEGIN
    IF EXISTS(SELECT FROM identity.revoked_tokens) THEN RAISE EXCEPTION 'Cross-issuer audit leak'; END IF;
END $$;
SELECT set_config('app.subject','',true), set_config('app.issuer','',true);
DO $$ BEGIN
    IF EXISTS(SELECT FROM identity.revoked_tokens) THEN RAISE EXCEPTION 'Missing-context audit leak'; END IF;
END $$;
ROLLBACK;
