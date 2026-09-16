BEGIN;
CREATE SCHEMA taxation;
REVOKE ALL ON SCHEMA taxation FROM PUBLIC;
CREATE TABLE taxation.tax_profiles(
  organization_id uuid NOT NULL REFERENCES organization.organizations,
  profile_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  code text NOT NULL,
  name text NOT NULL,
  country_code char(2) NOT NULL CHECK(country_code~'^[A-Z]{2}$'),
  prices_include_tax boolean NOT NULL,
  is_active boolean NOT NULL DEFAULT true,
  row_version bigint NOT NULL DEFAULT 1 CHECK(row_version>0),
  issuer text NOT NULL,
  subject text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,profile_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,code),
  CHECK(char_length(code) BETWEEN 1 AND 40),
  CHECK(char_length(name) BETWEEN 1 AND 120)
);
CREATE INDEX ix_tax_profiles_cursor ON taxation.tax_profiles(organization_id,profile_id);
CREATE TABLE taxation.tax_rates(
  organization_id uuid NOT NULL,
  rate_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  profile_id uuid NOT NULL,
  branch_id uuid NULL,
  category_code text NOT NULL,
  rate_percent numeric(9,6) NOT NULL CHECK(rate_percent BETWEEN 0 AND 100),
  effective_from timestamptz NOT NULL,
  effective_until timestamptz NULL,
  is_active boolean NOT NULL DEFAULT true,
  row_version bigint NOT NULL DEFAULT 1 CHECK(row_version>0),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,rate_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,profile_id) REFERENCES taxation.tax_profiles(organization_id,profile_id),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id),
  CHECK(char_length(category_code) BETWEEN 1 AND 40),
  CHECK(effective_until IS NULL OR effective_until>effective_from)
);
CREATE INDEX ix_tax_rates_resolve ON taxation.tax_rates(organization_id,profile_id,category_code,branch_id,effective_from);
ALTER TABLE taxation.tax_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE taxation.tax_profiles FORCE ROW LEVEL SECURITY;
ALTER TABLE taxation.tax_rates ENABLE ROW LEVEL SECURITY;
ALTER TABLE taxation.tax_rates FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON taxation.tax_profiles
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON taxation.tax_rates
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA taxation TO salekhpos_runtime;
GRANT SELECT ON taxation.tax_profiles,taxation.tax_rates TO salekhpos_runtime;
GRANT INSERT ON taxation.tax_profiles,taxation.tax_rates TO salekhpos_runtime;
GRANT UPDATE(name,country_code,prices_include_tax,is_active,row_version,updated_at) ON taxation.tax_profiles TO salekhpos_runtime;
GRANT UPDATE(is_active,row_version) ON taxation.tax_rates TO salekhpos_runtime;
COMMIT;
