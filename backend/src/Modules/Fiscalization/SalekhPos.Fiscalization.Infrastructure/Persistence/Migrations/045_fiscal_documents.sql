BEGIN;
DO $$ BEGIN
  IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
  IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN RAISE EXCEPTION 'Provision runtime role'; END IF;
END $$;

CREATE SCHEMA fiscalization;
REVOKE ALL ON SCHEMA fiscalization FROM PUBLIC;

CREATE TABLE fiscalization.documents(
  organization_id uuid NOT NULL,
  document_id uuid NOT NULL CHECK(document_id<>'00000000-0000-0000-0000-000000000000'),
  branch_id uuid NOT NULL,
  sale_id uuid NOT NULL,
  provider_key varchar(80) NOT NULL CHECK(provider_key=btrim(provider_key)),
  document_type varchar(40) NOT NULL CHECK(document_type=btrim(document_type)),
  currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
  gross_amount numeric(19,4) NOT NULL CHECK(gross_amount>=0),
  payload text NOT NULL CHECK(char_length(payload) BETWEEN 1 AND 262144),
  payload_sha256 char(64) NOT NULL CHECK(payload_sha256~'^[0-9a-f]{64}$'),
  status varchar(16) NOT NULL CHECK(status IN('pending','submitted','accepted','rejected')),
  provider_reference varchar(200),
  attempt_count integer NOT NULL DEFAULT 0 CHECK(attempt_count>=0),
  created_at timestamptz NOT NULL,
  updated_at timestamptz NOT NULL,
  PRIMARY KEY(organization_id,document_id),
  UNIQUE(organization_id,sale_id,document_type),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id),
  FOREIGN KEY(organization_id,sale_id) REFERENCES sales.completed_sales(organization_id,sale_id),
  CHECK(provider_reference IS NULL OR (provider_reference=btrim(provider_reference) AND char_length(provider_reference) BETWEEN 1 AND 200))
);

CREATE TABLE fiscalization.submission_attempts(
  organization_id uuid NOT NULL,
  document_id uuid NOT NULL,
  attempt_number integer NOT NULL CHECK(attempt_number>0),
  outcome varchar(16) NOT NULL CHECK(outcome IN('accepted','rejected','retryable')),
  provider_reference varchar(200),
  provider_code varchar(80),
  failure_reason varchar(1000),
  attempted_at timestamptz NOT NULL,
  retry_after timestamptz,
  PRIMARY KEY(organization_id,document_id,attempt_number),
  FOREIGN KEY(organization_id,document_id) REFERENCES fiscalization.documents(organization_id,document_id),
  CHECK(provider_reference IS NULL OR char_length(provider_reference) BETWEEN 1 AND 200),
  CHECK(provider_code IS NULL OR char_length(provider_code) BETWEEN 1 AND 80),
  CHECK(failure_reason IS NULL OR char_length(failure_reason) BETWEEN 1 AND 1000),
  CHECK((outcome='accepted' AND provider_reference IS NOT NULL AND failure_reason IS NULL)
    OR (outcome IN('rejected','retryable') AND failure_reason IS NOT NULL))
);

CREATE INDEX fiscal_documents_sale ON fiscalization.documents(organization_id,sale_id);
CREATE INDEX fiscal_documents_retry ON fiscalization.documents(organization_id,status,updated_at);
ALTER TABLE fiscalization.documents ENABLE ROW LEVEL SECURITY;
ALTER TABLE fiscalization.documents FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fiscalization.documents
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
ALTER TABLE fiscalization.submission_attempts ENABLE ROW LEVEL SECURITY;
ALTER TABLE fiscalization.submission_attempts FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fiscalization.submission_attempts
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

GRANT USAGE ON SCHEMA fiscalization TO salekhpos_runtime;
GRANT SELECT ON fiscalization.documents,fiscalization.submission_attempts TO salekhpos_runtime;
GRANT INSERT(organization_id,document_id,branch_id,sale_id,provider_key,document_type,currency,gross_amount,
 payload,payload_sha256,status,created_at,updated_at) ON fiscalization.documents TO salekhpos_runtime;
GRANT UPDATE(status,provider_reference,attempt_count,updated_at) ON fiscalization.documents TO salekhpos_runtime;
GRANT INSERT ON fiscalization.submission_attempts TO salekhpos_runtime;
COMMIT;
