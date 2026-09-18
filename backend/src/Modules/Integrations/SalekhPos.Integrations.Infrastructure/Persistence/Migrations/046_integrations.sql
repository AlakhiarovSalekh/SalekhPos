BEGIN;
CREATE SCHEMA integrations;
REVOKE ALL ON SCHEMA integrations FROM PUBLIC;

CREATE TABLE integrations.connections(
  organization_id uuid NOT NULL REFERENCES organization.organizations,
  connection_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  provider text NOT NULL,
  display_name text NOT NULL,
  endpoint text NOT NULL,
  secret_reference text NOT NULL,
  status text NOT NULL DEFAULT 'active' CHECK(status IN('active','disabled')),
  disabled_reason text NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,connection_id),
  UNIQUE(organization_id,operation_id),
  CHECK(char_length(provider) BETWEEN 1 AND 64),
  CHECK(char_length(display_name) BETWEEN 1 AND 160),
  CHECK(char_length(endpoint) BETWEEN 1 AND 2048 AND endpoint LIKE 'https://%'),
  CHECK(char_length(secret_reference) BETWEEN 1 AND 512),
  CHECK(secret_reference !~ '[[:space:]]'),
  CHECK((status='active' AND disabled_reason IS NULL) OR
        (status='disabled' AND char_length(disabled_reason) BETWEEN 1 AND 500))
);
COMMENT ON COLUMN integrations.connections.secret_reference IS 'Opaque reference to an external secret store; plaintext credentials are forbidden.';

CREATE TABLE integrations.connection_state_changes(
  organization_id uuid NOT NULL,
  change_id uuid NOT NULL,
  connection_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  from_status text NOT NULL CHECK(from_status IN('active','disabled')),
  to_status text NOT NULL CHECK(to_status IN('active','disabled')),
  reason text NOT NULL CHECK(char_length(reason) BETWEEN 1 AND 500),
  changed_by_issuer text NOT NULL,
  changed_by_subject text NOT NULL,
  changed_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,change_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,connection_id) REFERENCES integrations.connections(organization_id,connection_id)
);

CREATE TABLE integrations.webhook_deliveries(
  organization_id uuid NOT NULL,
  delivery_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  connection_id uuid NOT NULL,
  event_id uuid NOT NULL,
  event_type text NOT NULL,
  payload_sha256 char(64) NOT NULL CHECK(payload_sha256 ~ '^[0-9a-f]{64}$'),
  payload_reference text NOT NULL CHECK(char_length(payload_reference) BETWEEN 1 AND 512),
  status text NOT NULL DEFAULT 'pending' CHECK(status IN('pending','delivering','delivered','failed','dead_lettered')),
  attempt_count integer NOT NULL DEFAULT 0 CHECK(attempt_count BETWEEN 0 AND 100),
  lease_id uuid NULL,
  lease_expires_at timestamptz NULL,
  next_attempt_at timestamptz NULL,
  last_status_code integer NULL CHECK(last_status_code BETWEEN 100 AND 599),
  last_error_code text NULL CHECK(last_error_code IS NULL OR char_length(last_error_code) BETWEEN 1 AND 100),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,delivery_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,connection_id,event_id),
  FOREIGN KEY(organization_id,connection_id) REFERENCES integrations.connections(organization_id,connection_id),
  CHECK((status='delivering' AND lease_id IS NOT NULL AND lease_expires_at IS NOT NULL) OR
        (status<>'delivering' AND lease_id IS NULL AND lease_expires_at IS NULL))
);

CREATE TABLE integrations.webhook_attempts(
  organization_id uuid NOT NULL,
  attempt_id uuid NOT NULL,
  delivery_id uuid NOT NULL,
  attempt_number integer NOT NULL CHECK(attempt_number BETWEEN 1 AND 100),
  lease_id uuid NOT NULL,
  succeeded boolean NOT NULL,
  status_code integer NULL CHECK(status_code BETWEEN 100 AND 599),
  error_code text NULL CHECK(error_code IS NULL OR char_length(error_code) BETWEEN 1 AND 100),
  attempted_by_issuer text NOT NULL,
  attempted_by_subject text NOT NULL,
  attempted_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,attempt_id),
  UNIQUE(organization_id,delivery_id,lease_id),
  UNIQUE(organization_id,delivery_id,attempt_number),
  FOREIGN KEY(organization_id,delivery_id) REFERENCES integrations.webhook_deliveries(organization_id,delivery_id)
);

CREATE INDEX ix_integrations_connections_cursor ON integrations.connections(organization_id,connection_id);
CREATE INDEX ix_integrations_webhooks_cursor ON integrations.webhook_deliveries(organization_id,delivery_id);
CREATE INDEX ix_integrations_webhooks_due ON integrations.webhook_deliveries(status,next_attempt_at) WHERE status IN('pending','failed');

ALTER TABLE integrations.connections ENABLE ROW LEVEL SECURITY;
ALTER TABLE integrations.connections FORCE ROW LEVEL SECURITY;
ALTER TABLE integrations.connection_state_changes ENABLE ROW LEVEL SECURITY;
ALTER TABLE integrations.connection_state_changes FORCE ROW LEVEL SECURITY;
ALTER TABLE integrations.webhook_deliveries ENABLE ROW LEVEL SECURITY;
ALTER TABLE integrations.webhook_deliveries FORCE ROW LEVEL SECURITY;
ALTER TABLE integrations.webhook_attempts ENABLE ROW LEVEL SECURITY;
ALTER TABLE integrations.webhook_attempts FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON integrations.connections USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON integrations.connection_state_changes USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON integrations.webhook_deliveries USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON integrations.webhook_attempts USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

GRANT USAGE ON SCHEMA integrations TO salekhpos_runtime;
GRANT SELECT,INSERT ON integrations.connections,integrations.connection_state_changes,integrations.webhook_deliveries,integrations.webhook_attempts TO salekhpos_runtime;
GRANT UPDATE(status,disabled_reason,updated_at) ON integrations.connections TO salekhpos_runtime;
GRANT UPDATE(status,attempt_count,lease_id,lease_expires_at,next_attempt_at,last_status_code,last_error_code,updated_at) ON integrations.webhook_deliveries TO salekhpos_runtime;
COMMIT;
