BEGIN;
CREATE SCHEMA billing;
REVOKE ALL ON SCHEMA billing FROM PUBLIC;

CREATE TABLE billing.accounts(
  organization_id uuid NOT NULL REFERENCES organization.organizations,
  account_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  request_hash char(64) NOT NULL CHECK(request_hash~'^[0-9a-f]{64}$'),
  legal_name text NOT NULL CHECK(char_length(legal_name) BETWEEN 1 AND 160),
  billing_email text NOT NULL CHECK(char_length(billing_email) BETWEEN 3 AND 320),
  currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
  tax_identifier text NULL CHECK(tax_identifier IS NULL OR char_length(tax_identifier) BETWEEN 1 AND 80),
  issuer text NOT NULL,
  subject text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,account_id),
  UNIQUE(organization_id,operation_id)
);

CREATE TABLE billing.invoices(
  organization_id uuid NOT NULL,
  invoice_id uuid NOT NULL,
  account_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  request_hash char(64) NOT NULL CHECK(request_hash~'^[0-9a-f]{64}$'),
  invoice_number text NOT NULL CHECK(char_length(invoice_number) BETWEEN 1 AND 64),
  currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
  status text NOT NULL CHECK(status IN('open','partially_paid','paid','voided')),
  net_amount numeric(18,2) NOT NULL CHECK(net_amount>=0),
  tax_amount numeric(18,2) NOT NULL CHECK(tax_amount>=0),
  gross_amount numeric(18,2) NOT NULL CHECK(gross_amount=net_amount+tax_amount AND gross_amount>0),
  paid_amount numeric(18,2) NOT NULL DEFAULT 0 CHECK(paid_amount>=0 AND paid_amount<=gross_amount),
  credited_amount numeric(18,2) NOT NULL DEFAULT 0 CHECK(credited_amount>=0 AND credited_amount<=gross_amount),
  issued_at timestamptz NOT NULL,
  due_at timestamptz NOT NULL CHECK(due_at>=issued_at),
  issuer text NOT NULL,
  subject text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  row_version bigint NOT NULL DEFAULT 1 CHECK(row_version>0),
  PRIMARY KEY(organization_id,invoice_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,invoice_number),
  FOREIGN KEY(organization_id,account_id) REFERENCES billing.accounts(organization_id,account_id)
);

CREATE TABLE billing.invoice_lines(
  organization_id uuid NOT NULL,
  invoice_id uuid NOT NULL,
  line_id uuid NOT NULL,
  position integer NOT NULL CHECK(position BETWEEN 1 AND 500),
  description text NOT NULL CHECK(char_length(description) BETWEEN 1 AND 240),
  quantity bigint NOT NULL CHECK(quantity BETWEEN 1 AND 1000000),
  unit_amount numeric(18,2) NOT NULL CHECK(unit_amount>=0),
  tax_rate numeric(9,6) NOT NULL CHECK(tax_rate BETWEEN 0 AND 100),
  net_amount numeric(18,2) NOT NULL CHECK(net_amount>=0),
  tax_amount numeric(18,2) NOT NULL CHECK(tax_amount>=0),
  gross_amount numeric(18,2) NOT NULL CHECK(gross_amount=net_amount+tax_amount),
  PRIMARY KEY(organization_id,invoice_id,line_id),
  UNIQUE(organization_id,invoice_id,position),
  FOREIGN KEY(organization_id,invoice_id) REFERENCES billing.invoices(organization_id,invoice_id)
);

CREATE TABLE billing.charges(
  organization_id uuid NOT NULL,
  charge_id uuid NOT NULL,
  invoice_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  request_hash char(64) NOT NULL CHECK(request_hash~'^[0-9a-f]{64}$'),
  amount numeric(18,2) NOT NULL CHECK(amount>0),
  currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
  status text NOT NULL CHECK(status IN('succeeded','failed','refunded')),
  provider_reference text NULL CHECK(provider_reference IS NULL OR char_length(provider_reference) BETWEEN 1 AND 128),
  failure_code text NULL CHECK(failure_code IS NULL OR char_length(failure_code) BETWEEN 1 AND 64),
  issuer text NOT NULL,
  subject text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,charge_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,provider_reference),
  FOREIGN KEY(organization_id,invoice_id) REFERENCES billing.invoices(organization_id,invoice_id)
);

CREATE TABLE billing.credit_notes(
  organization_id uuid NOT NULL,
  credit_id uuid NOT NULL,
  invoice_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  request_hash char(64) NOT NULL CHECK(request_hash~'^[0-9a-f]{64}$'),
  amount numeric(18,2) NOT NULL CHECK(amount>0),
  currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
  reason text NOT NULL CHECK(char_length(reason) BETWEEN 1 AND 240),
  issuer text NOT NULL,
  subject text NOT NULL,
  issued_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,credit_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,invoice_id) REFERENCES billing.invoices(organization_id,invoice_id)
);

CREATE INDEX ix_billing_invoices_account ON billing.invoices(organization_id,account_id,issued_at DESC,invoice_id DESC);
CREATE INDEX ix_billing_charges_invoice ON billing.charges(organization_id,invoice_id,created_at,charge_id);
CREATE INDEX ix_billing_credits_invoice ON billing.credit_notes(organization_id,invoice_id,issued_at,credit_id);

ALTER TABLE billing.accounts ENABLE ROW LEVEL SECURITY; ALTER TABLE billing.accounts FORCE ROW LEVEL SECURITY;
ALTER TABLE billing.invoices ENABLE ROW LEVEL SECURITY; ALTER TABLE billing.invoices FORCE ROW LEVEL SECURITY;
ALTER TABLE billing.invoice_lines ENABLE ROW LEVEL SECURITY; ALTER TABLE billing.invoice_lines FORCE ROW LEVEL SECURITY;
ALTER TABLE billing.charges ENABLE ROW LEVEL SECURITY; ALTER TABLE billing.charges FORCE ROW LEVEL SECURITY;
ALTER TABLE billing.credit_notes ENABLE ROW LEVEL SECURITY; ALTER TABLE billing.credit_notes FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON billing.accounts USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON billing.invoices USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON billing.invoice_lines USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON billing.charges USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON billing.credit_notes USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA billing TO salekhpos_runtime;
GRANT SELECT,INSERT ON billing.accounts,billing.invoice_lines,billing.charges,billing.credit_notes TO salekhpos_runtime;
GRANT SELECT,INSERT ON billing.invoices TO salekhpos_runtime;
GRANT UPDATE(status,paid_amount,credited_amount,row_version) ON billing.invoices TO salekhpos_runtime;
GRANT UPDATE(status) ON billing.charges TO salekhpos_runtime;
COMMIT;
