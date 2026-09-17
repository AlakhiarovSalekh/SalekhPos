BEGIN;
CREATE SCHEMA support;
REVOKE ALL ON SCHEMA support FROM PUBLIC;

CREATE TABLE support.tickets(
  organization_id uuid NOT NULL REFERENCES organization.organizations,
  ticket_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  branch_id uuid NULL,
  subject text NOT NULL CHECK(char_length(subject) BETWEEN 1 AND 200),
  description text NOT NULL CHECK(char_length(description) BETWEEN 1 AND 8000),
  priority text NOT NULL CHECK(priority IN('low','normal','high','urgent')),
  status text NOT NULL DEFAULT 'open' CHECK(status IN('open','in_progress','waiting_for_customer','resolved','closed')),
  version integer NOT NULL DEFAULT 1 CHECK(version>0),
  opened_by_issuer text NOT NULL,
  opened_by_subject text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,ticket_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id)
);

CREATE TABLE support.ticket_transitions(
  organization_id uuid NOT NULL,
  transition_id uuid NOT NULL,
  ticket_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  from_status text NOT NULL,
  to_status text NOT NULL,
  note text NOT NULL CHECK(char_length(note) BETWEEN 1 AND 2000),
  transitioned_by_issuer text NOT NULL,
  transitioned_by_subject text NOT NULL,
  transitioned_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,transition_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,ticket_id) REFERENCES support.tickets(organization_id,ticket_id)
);

CREATE TABLE support.diagnostic_references(
  organization_id uuid NOT NULL,
  diagnostic_id uuid NOT NULL,
  ticket_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  kind text NOT NULL CHECK(char_length(kind) BETWEEN 1 AND 64),
  reference text NOT NULL CHECK(char_length(reference) BETWEEN 1 AND 512),
  sha256 char(64) NOT NULL CHECK(sha256 ~ '^[0-9a-f]{64}$'),
  added_by_issuer text NOT NULL,
  added_by_subject text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,diagnostic_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,ticket_id,sha256),
  FOREIGN KEY(organization_id,ticket_id) REFERENCES support.tickets(organization_id,ticket_id)
);

CREATE INDEX ix_support_tickets_cursor ON support.tickets(organization_id,ticket_id);
CREATE INDEX ix_support_tickets_status ON support.tickets(organization_id,status,ticket_id);
CREATE INDEX ix_support_diagnostics_ticket ON support.diagnostic_references(organization_id,ticket_id,created_at);

ALTER TABLE support.tickets ENABLE ROW LEVEL SECURITY;
ALTER TABLE support.tickets FORCE ROW LEVEL SECURITY;
ALTER TABLE support.ticket_transitions ENABLE ROW LEVEL SECURITY;
ALTER TABLE support.ticket_transitions FORCE ROW LEVEL SECURITY;
ALTER TABLE support.diagnostic_references ENABLE ROW LEVEL SECURITY;
ALTER TABLE support.diagnostic_references FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON support.tickets USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON support.ticket_transitions USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON support.diagnostic_references USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

GRANT USAGE ON SCHEMA support TO salekhpos_runtime;
GRANT SELECT,INSERT ON support.tickets,support.ticket_transitions,support.diagnostic_references TO salekhpos_runtime;
GRANT UPDATE(status,version,updated_at) ON support.tickets TO salekhpos_runtime;
COMMIT;
