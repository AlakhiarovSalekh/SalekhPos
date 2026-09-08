BEGIN;
DO $$ BEGIN
  IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
  IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN RAISE EXCEPTION 'Provision runtime role'; END IF;
END $$;
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE SCHEMA pricing;
REVOKE ALL ON SCHEMA pricing FROM PUBLIC;
CREATE TABLE pricing.prices(
  organization_id uuid NOT NULL,
  price_id uuid NOT NULL CHECK(price_id<>'00000000-0000-0000-0000-000000000000'),
  operation_id uuid NOT NULL CHECK(operation_id<>'00000000-0000-0000-0000-000000000000'),
  product_id uuid NOT NULL,
  branch_id uuid,
  amount numeric(20,6) NOT NULL CHECK(amount>0),
  currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
  tax_mode text NOT NULL CHECK(tax_mode IN('inclusive','exclusive')),
  tax_rate numeric(7,4) NOT NULL CHECK(tax_rate BETWEEN 0 AND 100),
  valid_from timestamptz NOT NULL,
  valid_until timestamptz,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  issuer text NOT NULL CHECK(char_length(issuer) BETWEEN 1 AND 2048),
  subject text NOT NULL CHECK(char_length(subject) BETWEEN 1 AND 256),
  PRIMARY KEY(organization_id,price_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,product_id) REFERENCES catalog.products(organization_id,product_id),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id),
  CHECK(valid_until IS NULL OR valid_until>valid_from),
  EXCLUDE USING gist(organization_id WITH =,product_id WITH =,
    (coalesce(branch_id,'00000000-0000-0000-0000-000000000000'::uuid)) WITH =,
    (tstzrange(valid_from,valid_until,'[)')) WITH &&)
);
CREATE INDEX prices_resolution ON pricing.prices(organization_id,product_id,branch_id,valid_from DESC);
ALTER TABLE pricing.prices ENABLE ROW LEVEL SECURITY;
ALTER TABLE pricing.prices FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON pricing.prices
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA pricing TO salekhpos_runtime;
GRANT SELECT ON pricing.prices TO salekhpos_runtime;
GRANT INSERT(organization_id,price_id,operation_id,product_id,branch_id,amount,currency,tax_mode,tax_rate,valid_from,valid_until,issuer,subject)
 ON pricing.prices TO salekhpos_runtime;
COMMIT;
