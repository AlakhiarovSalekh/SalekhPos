-- Identity-owned credential revocation and its immutable audit record are one
-- atomic row. No raw bearer credentials, provider keys or refresh tokens persist.
BEGIN;
CREATE SCHEMA identity;
REVOKE ALL ON SCHEMA identity FROM PUBLIC;
CREATE TABLE identity.revoked_tokens (
    fingerprint bytea PRIMARY KEY CHECK (octet_length(fingerprint) = 32),
    issuer text NOT NULL CHECK (char_length(issuer) BETWEEN 1 AND 2048),
    subject text NOT NULL CHECK (char_length(subject) BETWEEN 1 AND 256),
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz NOT NULL DEFAULT statement_timestamp(),
    action text NOT NULL DEFAULT 'identity.token_revoked' CHECK (action = 'identity.token_revoked'),
    trace_id text NOT NULL CHECK (trace_id ~ '^[a-fA-F0-9]{32}$')
);
ALTER TABLE identity.revoked_tokens ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.revoked_tokens FORCE ROW LEVEL SECURITY;
CREATE POLICY own_credential ON identity.revoked_tokens
    USING (issuer = current_setting('app.issuer', true) AND subject = current_setting('app.subject', true))
    WITH CHECK (issuer = current_setting('app.issuer', true) AND subject = current_setting('app.subject', true));
GRANT USAGE ON SCHEMA identity TO salekhpos_runtime;
GRANT SELECT ON identity.revoked_tokens TO salekhpos_runtime;
GRANT INSERT (fingerprint,issuer,subject,expires_at,trace_id) ON identity.revoked_tokens TO salekhpos_runtime;
COMMIT;
