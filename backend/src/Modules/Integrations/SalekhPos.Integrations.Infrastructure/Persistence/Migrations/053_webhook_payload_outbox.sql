BEGIN;

CREATE TABLE integrations.webhook_payloads(
  organization_id uuid NOT NULL,
  delivery_id uuid NOT NULL,
  payload_sha256 char(64) NOT NULL CHECK(payload_sha256 ~ '^[0-9a-f]{64}$'),
  payload bytea NOT NULL CHECK(octet_length(payload) BETWEEN 1 AND 262144),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,delivery_id),
  FOREIGN KEY(organization_id,delivery_id)
    REFERENCES integrations.webhook_deliveries(organization_id,delivery_id),
  CHECK(octet_length(payload_sha256)=64)
);

COMMENT ON TABLE integrations.webhook_payloads IS
  'Immutable tenant-scoped webhook payloads for the built-in PostgreSQL outbox provider.';

ALTER TABLE integrations.webhook_payloads ENABLE ROW LEVEL SECURITY;
ALTER TABLE integrations.webhook_payloads FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON integrations.webhook_payloads
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

REVOKE ALL ON integrations.webhook_payloads FROM PUBLIC;
GRANT SELECT,INSERT ON integrations.webhook_payloads TO salekhpos_runtime;

COMMIT;
