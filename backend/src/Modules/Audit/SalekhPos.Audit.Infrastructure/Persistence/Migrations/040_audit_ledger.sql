BEGIN;
DO $$ BEGIN
  IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
  IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN RAISE EXCEPTION 'Provision runtime role'; END IF;
END $$;

CREATE SCHEMA audit;
REVOKE ALL ON SCHEMA audit FROM PUBLIC;

CREATE TABLE audit.stream_heads(
  organization_id uuid PRIMARY KEY REFERENCES organization.organizations(organization_id),
  last_sequence bigint NOT NULL DEFAULT 0 CHECK(last_sequence>=0),
  last_hash bytea NOT NULL DEFAULT decode(repeat('00',32),'hex') CHECK(octet_length(last_hash)=32),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp()
);

CREATE TABLE audit.events(
  organization_id uuid NOT NULL,
  event_id uuid NOT NULL CHECK(event_id<>'00000000-0000-0000-0000-000000000000'),
  operation_id uuid NOT NULL CHECK(operation_id<>'00000000-0000-0000-0000-000000000000'),
  sequence bigint NOT NULL CHECK(sequence>0),
  actor_issuer text NOT NULL CHECK(char_length(actor_issuer) BETWEEN 1 AND 2048),
  actor_subject text NOT NULL CHECK(char_length(actor_subject) BETWEEN 1 AND 256),
  action varchar(180) NOT NULL CHECK(action=btrim(action)),
  target_type varchar(80) NOT NULL CHECK(target_type=btrim(target_type)),
  target_id uuid,
  branch_id uuid,
  device_id uuid,
  source_ip varchar(64),
  outcome varchar(16) NOT NULL CHECK(outcome IN('attempted','succeeded','failed')),
  reason varchar(400),
  correlation_id varchar(128) NOT NULL,
  request_id varchar(128) NOT NULL,
  occurred_at timestamptz NOT NULL,
  previous_hash bytea NOT NULL CHECK(octet_length(previous_hash)=32),
  event_hash bytea NOT NULL CHECK(octet_length(event_hash)=32),
  PRIMARY KEY(organization_id,event_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,sequence),
  FOREIGN KEY(organization_id) REFERENCES organization.organizations(organization_id),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id),
  CHECK(target_id IS NULL OR target_id<>'00000000-0000-0000-0000-000000000000'),
  CHECK(branch_id IS NULL OR branch_id<>'00000000-0000-0000-0000-000000000000'),
  CHECK(device_id IS NULL OR device_id<>'00000000-0000-0000-0000-000000000000'),
  CHECK(source_ip IS NULL OR (source_ip=btrim(source_ip) AND char_length(source_ip) BETWEEN 1 AND 64)),
  CHECK(reason IS NULL OR (reason=btrim(reason) AND char_length(reason) BETWEEN 1 AND 400)),
  CHECK(correlation_id=btrim(correlation_id) AND char_length(correlation_id) BETWEEN 1 AND 128),
  CHECK(request_id=btrim(request_id) AND char_length(request_id) BETWEEN 1 AND 128)
);

CREATE INDEX audit_events_sequence ON audit.events(organization_id,sequence);
CREATE INDEX audit_events_action ON audit.events(organization_id,action,sequence);
CREATE INDEX audit_events_branch ON audit.events(organization_id,branch_id,sequence);
ALTER TABLE audit.stream_heads ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit.stream_heads FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON audit.stream_heads
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

ALTER TABLE audit.events ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit.events FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON audit.events
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

GRANT USAGE ON SCHEMA audit TO salekhpos_runtime;
GRANT SELECT ON audit.stream_heads,audit.events TO salekhpos_runtime;
GRANT INSERT(organization_id,last_sequence,last_hash) ON audit.stream_heads TO salekhpos_runtime;
GRANT UPDATE(last_sequence,last_hash,updated_at) ON audit.stream_heads TO salekhpos_runtime;
GRANT INSERT(organization_id,event_id,operation_id,sequence,actor_issuer,actor_subject,action,target_type,
  target_id,branch_id,device_id,source_ip,outcome,reason,correlation_id,request_id,occurred_at,previous_hash,event_hash)
  ON audit.events TO salekhpos_runtime;
COMMIT;
