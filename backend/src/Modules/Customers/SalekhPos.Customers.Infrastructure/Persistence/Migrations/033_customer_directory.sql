BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE SCHEMA customers;
REVOKE ALL ON SCHEMA customers FROM PUBLIC;
CREATE TABLE customers.customers(
  organization_id uuid NOT NULL,
  customer_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  code varchar(40) NOT NULL,
  display_name varchar(160) NOT NULL,
  email varchar(254),
  phone varchar(32),
  is_active boolean NOT NULL DEFAULT true,
  row_version bigint NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  issuer text NOT NULL,
  subject text NOT NULL,
  PRIMARY KEY(organization_id,customer_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,code),
  FOREIGN KEY(organization_id) REFERENCES organization.organizations(organization_id),
  CHECK(code~'^[A-Za-z0-9_-]{1,40}$'),
  CHECK(char_length(display_name) BETWEEN 1 AND 160),
  CHECK(email IS NULL OR char_length(email) BETWEEN 3 AND 254),
  CHECK(phone IS NULL OR char_length(phone) BETWEEN 3 AND 32),
  CHECK(row_version>=1)
);
CREATE INDEX ix_customers_cursor ON customers.customers(organization_id,customer_id);
ALTER TABLE customers.customers ENABLE ROW LEVEL SECURITY;
ALTER TABLE customers.customers FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON customers.customers
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA customers TO salekhpos_runtime;
GRANT SELECT ON customers.customers TO salekhpos_runtime;
GRANT INSERT(organization_id,customer_id,operation_id,code,display_name,email,phone,is_active,issuer,subject)
  ON customers.customers TO salekhpos_runtime;
GRANT UPDATE(display_name,email,phone,is_active,row_version,updated_at)
  ON customers.customers TO salekhpos_runtime;
COMMIT;
