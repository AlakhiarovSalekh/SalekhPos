BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE TABLE devices.request_proof_nonces(
 organization_id uuid NOT NULL,
 device_id uuid NOT NULL,
 credential_id uuid NOT NULL,
 nonce_hash bytea NOT NULL,
 request_timestamp timestamptz NOT NULL,
 accepted_at timestamptz NOT NULL,
 expires_at timestamptz NOT NULL,
 PRIMARY KEY(organization_id,device_id,credential_id,nonce_hash),
 FOREIGN KEY(organization_id,device_id,credential_id) REFERENCES devices.device_credentials(organization_id,device_id,credential_id),
 CONSTRAINT request_proof_nonces_hash_length CHECK(octet_length(nonce_hash)=32),
 CONSTRAINT request_proof_nonces_timestamp_window CHECK(request_timestamp BETWEEN accepted_at-interval '5 minutes' AND accepted_at+interval '5 minutes'),
 CONSTRAINT request_proof_nonces_retention CHECK(expires_at=accepted_at+interval '11 minutes')
);
CREATE INDEX ix_request_proof_nonces_expiry ON devices.request_proof_nonces(organization_id,expires_at);
ALTER TABLE devices.request_proof_nonces ENABLE ROW LEVEL SECURITY;
ALTER TABLE devices.request_proof_nonces FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON devices.request_proof_nonces
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE FUNCTION devices.reject_live_request_proof_nonce_deletion() RETURNS trigger
 LANGUAGE plpgsql SET search_path=pg_catalog AS $$
BEGIN
 IF OLD.expires_at>statement_timestamp() THEN RAISE EXCEPTION 'Live device request nonce evidence is immutable' USING ERRCODE='42501'; END IF;
 RETURN OLD;
END $$;
REVOKE ALL ON FUNCTION devices.reject_live_request_proof_nonce_deletion() FROM PUBLIC;
CREATE TRIGGER protect_live_request_proof_nonces BEFORE DELETE ON devices.request_proof_nonces
 FOR EACH ROW EXECUTE FUNCTION devices.reject_live_request_proof_nonce_deletion();
CREATE FUNCTION devices.purge_expired_request_proof_nonces() RETURNS bigint
 LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog AS $$
 WITH purged AS(
  DELETE FROM devices.request_proof_nonces
  WHERE organization_id=nullif(current_setting('app.organization_id',true),'')::uuid
    AND expires_at<=statement_timestamp()
  RETURNING 1
 ) SELECT count(*) FROM purged
$$;
REVOKE ALL ON FUNCTION devices.purge_expired_request_proof_nonces() FROM PUBLIC;
GRANT INSERT(organization_id,device_id,credential_id,nonce_hash,request_timestamp,accepted_at,expires_at)
 ON devices.request_proof_nonces TO salekhpos_runtime;
GRANT EXECUTE ON FUNCTION devices.purge_expired_request_proof_nonces() TO salekhpos_runtime;
COMMIT;
