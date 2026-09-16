BEGIN;
CREATE SCHEMA notifications;
REVOKE ALL ON SCHEMA notifications FROM PUBLIC;
CREATE TABLE notifications.inbox(
  organization_id uuid NOT NULL REFERENCES organization.organizations,
  notification_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  branch_id uuid NULL,
  recipient_subject text NOT NULL,
  title text NOT NULL,
  body text NOT NULL,
  severity text NOT NULL CHECK(severity IN('info','warning','critical')),
  is_read boolean NOT NULL DEFAULT false,
  read_at timestamptz NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,notification_id),
  UNIQUE(organization_id,operation_id),
  CHECK(char_length(recipient_subject) BETWEEN 1 AND 256),
  CHECK(char_length(title) BETWEEN 1 AND 160),
  CHECK(char_length(body) BETWEEN 1 AND 2000),
  CHECK((is_read AND read_at IS NOT NULL) OR (NOT is_read AND read_at IS NULL)),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id)
);
CREATE INDEX ix_notifications_recipient_cursor ON notifications.inbox(organization_id,recipient_subject,notification_id);
CREATE TABLE notifications.preferences(
  organization_id uuid NOT NULL REFERENCES organization.organizations,
  issuer text NOT NULL,
  subject text NOT NULL,
  in_app_enabled boolean NOT NULL DEFAULT true,
  email_enabled boolean NOT NULL DEFAULT false,
  push_enabled boolean NOT NULL DEFAULT false,
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,issuer,subject)
);
ALTER TABLE notifications.inbox ENABLE ROW LEVEL SECURITY;
ALTER TABLE notifications.inbox FORCE ROW LEVEL SECURITY;
ALTER TABLE notifications.preferences ENABLE ROW LEVEL SECURITY;
ALTER TABLE notifications.preferences FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON notifications.inbox
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON notifications.preferences
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA notifications TO salekhpos_runtime;
GRANT SELECT ON notifications.inbox,notifications.preferences TO salekhpos_runtime;
GRANT INSERT ON notifications.inbox,notifications.preferences TO salekhpos_runtime;
GRANT UPDATE(is_read,read_at) ON notifications.inbox TO salekhpos_runtime;
GRANT UPDATE(in_app_enabled,email_enabled,push_enabled,updated_at) ON notifications.preferences TO salekhpos_runtime;
COMMIT;
