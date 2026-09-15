BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE SCHEMA suppliers;
REVOKE ALL ON SCHEMA suppliers FROM PUBLIC;
CREATE TABLE suppliers.suppliers(
  organization_id uuid NOT NULL,
  supplier_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  code varchar(40) NOT NULL,
  name varchar(180) NOT NULL,
  tax_id varchar(64),
  email varchar(254),
  phone varchar(32),
  is_active boolean NOT NULL DEFAULT true,
  row_version bigint NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  issuer text NOT NULL,
  subject text NOT NULL,
  PRIMARY KEY(organization_id,supplier_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,code),
  FOREIGN KEY(organization_id) REFERENCES organization.organizations(organization_id),
  CHECK(code~'^[A-Za-z0-9_-]{1,40}$'),
  CHECK(char_length(name) BETWEEN 1 AND 180),
  CHECK(tax_id IS NULL OR char_length(tax_id) BETWEEN 1 AND 64),
  CHECK(email IS NULL OR char_length(email) BETWEEN 3 AND 254),
  CHECK(phone IS NULL OR char_length(phone) BETWEEN 3 AND 32),
  CHECK(row_version>=1)
);
CREATE INDEX ix_suppliers_cursor ON suppliers.suppliers(organization_id,supplier_id);
ALTER TABLE suppliers.suppliers ENABLE ROW LEVEL SECURITY;
ALTER TABLE suppliers.suppliers FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON suppliers.suppliers
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA suppliers TO salekhpos_runtime;
GRANT SELECT ON suppliers.suppliers TO salekhpos_runtime;
GRANT INSERT(organization_id,supplier_id,operation_id,code,name,tax_id,email,phone,is_active,issuer,subject)
  ON suppliers.suppliers TO salekhpos_runtime;
GRANT UPDATE(name,tax_id,email,phone,is_active,row_version,updated_at)
  ON suppliers.suppliers TO salekhpos_runtime;
COMMIT;
