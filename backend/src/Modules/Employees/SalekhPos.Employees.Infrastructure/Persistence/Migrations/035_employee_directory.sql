BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE SCHEMA IF NOT EXISTS employees;
REVOKE ALL ON SCHEMA employees FROM PUBLIC;

CREATE TABLE employees.employees(
  organization_id uuid NOT NULL,
  employee_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  code varchar(32) NOT NULL,
  display_name varchar(160) NOT NULL,
  email varchar(254),
  phone varchar(40),
  job_title varchar(100) NOT NULL,
  is_active boolean NOT NULL DEFAULT true,
  row_version bigint NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL,
  updated_at timestamptz NOT NULL,
  issuer text NOT NULL,
  subject text NOT NULL,
  PRIMARY KEY(organization_id,employee_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,code),
  CHECK(code ~ '^[A-Z0-9_-]{1,32}$'),
  CHECK(char_length(display_name) BETWEEN 1 AND 160 AND display_name=btrim(display_name)),
  CHECK(email IS NULL OR char_length(email) BETWEEN 3 AND 254),
  CHECK(phone IS NULL OR char_length(phone) BETWEEN 1 AND 40),
  CHECK(char_length(job_title) BETWEEN 1 AND 100 AND job_title=btrim(job_title)),
  CHECK(row_version >= 1)
);
CREATE INDEX ix_employees_cursor ON employees.employees(organization_id,employee_id);

CREATE TABLE employees.store_assignments(
  organization_id uuid NOT NULL,
  employee_id uuid NOT NULL,
  branch_id uuid NOT NULL,
  assigned_at timestamptz NOT NULL,
  PRIMARY KEY(organization_id,employee_id,branch_id),
  FOREIGN KEY(organization_id,employee_id) REFERENCES employees.employees(organization_id,employee_id),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id)
);
CREATE INDEX ix_employee_assignments_branch ON employees.store_assignments(organization_id,branch_id,employee_id);
ALTER TABLE employees.employees ENABLE ROW LEVEL SECURITY;
ALTER TABLE employees.employees FORCE ROW LEVEL SECURITY;
ALTER TABLE employees.store_assignments ENABLE ROW LEVEL SECURITY;
ALTER TABLE employees.store_assignments FORCE ROW LEVEL SECURITY;
CREATE POLICY employees_tenant ON employees.employees
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY employee_assignments_tenant ON employees.store_assignments
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

GRANT USAGE ON SCHEMA employees TO salekhpos_runtime;
GRANT SELECT ON employees.employees,employees.store_assignments TO salekhpos_runtime;
GRANT INSERT(organization_id,employee_id,operation_id,code,display_name,email,phone,job_title,is_active,
  row_version,created_at,updated_at,issuer,subject) ON employees.employees TO salekhpos_runtime;
GRANT UPDATE(display_name,email,phone,job_title,is_active,row_version,updated_at)
  ON employees.employees TO salekhpos_runtime;
GRANT INSERT(organization_id,employee_id,branch_id,assigned_at) ON employees.store_assignments TO salekhpos_runtime;
REVOKE DELETE ON employees.employees,employees.store_assignments FROM salekhpos_runtime;
COMMIT;
