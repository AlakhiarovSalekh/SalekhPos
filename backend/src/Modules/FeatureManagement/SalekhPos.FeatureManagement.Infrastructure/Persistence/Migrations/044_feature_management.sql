BEGIN;
DO $$ BEGIN IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN
  CREATE ROLE salekhpos_runtime NOLOGIN; END IF; END $$;
CREATE SCHEMA IF NOT EXISTS feature_management;
REVOKE ALL ON SCHEMA feature_management FROM PUBLIC;
GRANT USAGE ON SCHEMA feature_management TO salekhpos_runtime;

CREATE TABLE feature_management.features(
  feature_key text PRIMARY KEY CHECK(feature_key ~ '^[a-z0-9._-]{1,100}$'),
  default_enabled boolean NOT NULL DEFAULT false,
  emergency_disabled boolean NOT NULL DEFAULT false,
  rollout_percentage integer NOT NULL DEFAULT 0 CHECK(rollout_percentage BETWEEN 0 AND 100),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp()
);
CREATE TABLE feature_management.organization_entitlements(
  organization_id uuid NOT NULL REFERENCES organization.organizations(organization_id),
  feature_key text NOT NULL REFERENCES feature_management.features(feature_key),
  enabled boolean NOT NULL,
  source text NOT NULL CHECK(length(source) BETWEEN 1 AND 100),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,feature_key)
);
CREATE TABLE feature_management.organization_overrides(
  organization_id uuid NOT NULL REFERENCES organization.organizations(organization_id),
  feature_key text NOT NULL REFERENCES feature_management.features(feature_key),
  enabled boolean NOT NULL,
  reason text NOT NULL CHECK(length(reason) BETWEEN 1 AND 500),
  updated_by_issuer text NOT NULL CHECK(length(updated_by_issuer) BETWEEN 1 AND 2048),
  updated_by_subject text NOT NULL CHECK(length(updated_by_subject) BETWEEN 1 AND 256),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,feature_key)
);
ALTER TABLE feature_management.organization_entitlements ENABLE ROW LEVEL SECURITY;
ALTER TABLE feature_management.organization_entitlements FORCE ROW LEVEL SECURITY;
ALTER TABLE feature_management.organization_overrides ENABLE ROW LEVEL SECURITY;
ALTER TABLE feature_management.organization_overrides FORCE ROW LEVEL SECURITY;
CREATE POLICY organization_entitlements_scope ON feature_management.organization_entitlements
  USING(organization_id=NULLIF(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=NULLIF(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY organization_overrides_scope ON feature_management.organization_overrides
  USING(organization_id=NULLIF(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=NULLIF(current_setting('app.organization_id',true),'')::uuid);
GRANT SELECT ON feature_management.features TO salekhpos_runtime;
GRANT SELECT ON feature_management.organization_entitlements TO salekhpos_runtime;
GRANT SELECT,INSERT,UPDATE ON feature_management.organization_overrides TO salekhpos_runtime;
COMMIT;
