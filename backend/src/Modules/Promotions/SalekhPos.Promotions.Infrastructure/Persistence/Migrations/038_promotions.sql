BEGIN;
DO $$ BEGIN
  IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
  IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN RAISE EXCEPTION 'Provision runtime role'; END IF;
END $$;
CREATE SCHEMA promotions;
REVOKE ALL ON SCHEMA promotions FROM PUBLIC;
CREATE TABLE promotions.promotions(
  organization_id uuid NOT NULL,
  promotion_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  code varchar(40) NOT NULL,
  name varchar(160) NOT NULL,
  branch_id uuid,
  discount_kind varchar(16) NOT NULL CHECK(discount_kind IN('percentage','fixed')),
  discount_value numeric(20,6) NOT NULL CHECK(discount_value>0),
  currency char(3),
  minimum_subtotal numeric(20,6) NOT NULL CHECK(minimum_subtotal>=0),
  starts_at timestamptz NOT NULL,
  ends_at timestamptz,
  is_active boolean NOT NULL DEFAULT true,
  row_version bigint NOT NULL DEFAULT 1 CHECK(row_version>0),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  issuer text NOT NULL,
  subject text NOT NULL,
  PRIMARY KEY(organization_id,promotion_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,code),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id),
  CHECK(promotion_id<>'00000000-0000-0000-0000-000000000000'),
  CHECK(operation_id<>'00000000-0000-0000-0000-000000000000'),
  CHECK(code=btrim(code) AND name=btrim(name)),
  CHECK(currency IS NULL OR currency ~ '^[A-Z]{3}$'),
  CHECK((discount_kind='percentage' AND discount_value<=100)
     OR (discount_kind='fixed' AND currency IS NOT NULL)),
  CHECK(ends_at IS NULL OR ends_at>starts_at)
);
CREATE INDEX promotions_resolve ON promotions.promotions
  (organization_id,branch_id,is_active,starts_at,ends_at);
ALTER TABLE promotions.promotions ENABLE ROW LEVEL SECURITY;
ALTER TABLE promotions.promotions FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON promotions.promotions
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA promotions TO salekhpos_runtime;
GRANT SELECT ON promotions.promotions TO salekhpos_runtime;
GRANT INSERT(organization_id,promotion_id,operation_id,code,name,branch_id,discount_kind,discount_value,
  currency,minimum_subtotal,starts_at,ends_at,is_active,issuer,subject) ON promotions.promotions TO salekhpos_runtime;
GRANT UPDATE(is_active,row_version,updated_at) ON promotions.promotions TO salekhpos_runtime;
COMMIT;
