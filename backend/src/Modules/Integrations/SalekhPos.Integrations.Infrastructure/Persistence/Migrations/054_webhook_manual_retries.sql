BEGIN;

CREATE TABLE integrations.webhook_manual_retries(
  organization_id uuid NOT NULL,
  retry_id uuid NOT NULL,
  delivery_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  reason text NOT NULL CHECK(char_length(reason) BETWEEN 1 AND 500 AND btrim(reason)<>''),
  previous_attempt_count integer NOT NULL CHECK(previous_attempt_count BETWEEN 0 AND 100),
  changed_by_issuer text NOT NULL CHECK(char_length(changed_by_issuer) BETWEEN 1 AND 2048),
  changed_by_subject text NOT NULL CHECK(char_length(changed_by_subject) BETWEEN 1 AND 256),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,retry_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,delivery_id)
    REFERENCES integrations.webhook_deliveries(organization_id,delivery_id)
);

CREATE INDEX ix_webhook_manual_retries_delivery
  ON integrations.webhook_manual_retries(organization_id,delivery_id,created_at);

ALTER TABLE integrations.webhook_manual_retries ENABLE ROW LEVEL SECURITY;
ALTER TABLE integrations.webhook_manual_retries FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON integrations.webhook_manual_retries
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

REVOKE ALL ON integrations.webhook_manual_retries FROM PUBLIC;
GRANT SELECT,INSERT ON integrations.webhook_manual_retries TO salekhpos_runtime;

COMMIT;
