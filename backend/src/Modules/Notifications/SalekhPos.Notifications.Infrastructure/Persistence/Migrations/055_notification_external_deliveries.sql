BEGIN;

CREATE TABLE notifications.external_deliveries(
  organization_id uuid NOT NULL,
  delivery_id uuid NOT NULL,
  notification_id uuid NOT NULL,
  channel text NOT NULL CHECK(channel IN('email','push')),
  recipient_subject text NOT NULL CHECK(char_length(recipient_subject) BETWEEN 1 AND 256),
  status text NOT NULL DEFAULT 'pending'
    CHECK(status IN('pending','delivering','failed','delivered','dead_lettered')),
  attempt_count integer NOT NULL DEFAULT 0 CHECK(attempt_count BETWEEN 0 AND 10),
  next_attempt_at timestamptz NULL,
  lease_id uuid NULL,
  lease_expires_at timestamptz NULL,
  last_error_code text NULL CHECK(last_error_code IS NULL OR char_length(last_error_code) BETWEEN 1 AND 100),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,delivery_id),
  UNIQUE(organization_id,notification_id,channel),
  FOREIGN KEY(organization_id,notification_id)
    REFERENCES notifications.inbox(organization_id,notification_id),
  CHECK(
    (status='delivering' AND lease_id IS NOT NULL AND lease_expires_at IS NOT NULL)
    OR (status<>'delivering' AND lease_id IS NULL AND lease_expires_at IS NULL)
  ),
  CHECK(status NOT IN('delivered','dead_lettered') OR next_attempt_at IS NULL)
);

CREATE INDEX ix_notification_external_due
  ON notifications.external_deliveries(organization_id,status,next_attempt_at,created_at,delivery_id);

ALTER TABLE notifications.external_deliveries ENABLE ROW LEVEL SECURITY;
ALTER TABLE notifications.external_deliveries FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON notifications.external_deliveries
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

REVOKE ALL ON notifications.external_deliveries FROM PUBLIC;
GRANT SELECT,INSERT ON notifications.external_deliveries TO salekhpos_runtime;
GRANT UPDATE(status,attempt_count,next_attempt_at,lease_id,lease_expires_at,last_error_code,updated_at)
  ON notifications.external_deliveries TO salekhpos_runtime;

COMMIT;
