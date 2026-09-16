BEGIN;
CREATE SCHEMA localization;
REVOKE ALL ON SCHEMA localization FROM PUBLIC;

CREATE TABLE localization.organization_settings(
  organization_id uuid PRIMARY KEY REFERENCES organization.organizations,
  country_code char(2) NOT NULL CHECK(country_code~'^[A-Z]{2}$'),
  default_locale text NOT NULL,
  default_currency char(3) NOT NULL CHECK(default_currency~'^[A-Z]{3}$'),
  time_zone text NOT NULL,
  supported_locales text[] NOT NULL,
  first_day_of_week smallint NOT NULL CHECK(first_day_of_week BETWEEN 1 AND 7),
  row_version bigint NOT NULL DEFAULT 1 CHECK(row_version>0),
  issuer text NOT NULL,
  subject text NOT NULL,
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  CHECK(char_length(default_locale) BETWEEN 2 AND 35),
  CHECK(char_length(time_zone) BETWEEN 1 AND 100),
  CHECK(cardinality(supported_locales) BETWEEN 1 AND 20),
  CHECK(default_locale=ANY(supported_locales))
);
CREATE TABLE localization.operations(
  organization_id uuid NOT NULL REFERENCES organization.organizations,
  operation_id uuid NOT NULL,
  country_code char(2) NOT NULL,
  default_locale text NOT NULL,
  default_currency char(3) NOT NULL,
  time_zone text NOT NULL,
  supported_locales text[] NOT NULL,
  first_day_of_week smallint NOT NULL,
  expected_version bigint NULL,
  version_after bigint NOT NULL CHECK(version_after>0),
  applied boolean NOT NULL,
  occurred_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at_after timestamptz NOT NULL,
  PRIMARY KEY(organization_id,operation_id)
);

ALTER TABLE localization.organization_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE localization.organization_settings FORCE ROW LEVEL SECURITY;
ALTER TABLE localization.operations ENABLE ROW LEVEL SECURITY;
ALTER TABLE localization.operations FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON localization.organization_settings
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON localization.operations
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

GRANT USAGE ON SCHEMA localization TO salekhpos_runtime;
GRANT SELECT ON localization.organization_settings,localization.operations TO salekhpos_runtime;
GRANT INSERT ON localization.organization_settings,localization.operations TO salekhpos_runtime;
GRANT UPDATE(country_code,default_locale,default_currency,time_zone,supported_locales,
  first_day_of_week,row_version,issuer,subject,updated_at)
  ON localization.organization_settings TO salekhpos_runtime;
COMMIT;
