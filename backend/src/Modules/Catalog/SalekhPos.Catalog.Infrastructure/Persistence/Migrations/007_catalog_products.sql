BEGIN;
DO $$ BEGIN
    IF current_user = 'salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN
        RAISE EXCEPTION 'Provision the restricted runtime role before migrating';
    END IF;
END $$;

CREATE SCHEMA catalog;
REVOKE ALL ON SCHEMA catalog FROM PUBLIC;
CREATE TABLE catalog.products (
    organization_id uuid NOT NULL REFERENCES organization.organizations,
    product_id uuid NOT NULL CHECK(product_id <> '00000000-0000-0000-0000-000000000000'),
    operation_id uuid NOT NULL CHECK(operation_id <> '00000000-0000-0000-0000-000000000000'),
    sku text NOT NULL CHECK(sku ~ '^[A-Z0-9][A-Z0-9._-]{0,63}$'),
    name text NOT NULL CHECK(name=btrim(name) AND char_length(name) BETWEEN 1 AND 200),
    unit_code text NOT NULL CHECK(unit_code ~ '^[A-Z0-9][A-Z0-9_-]{0,15}$'),
    barcode text CHECK(barcode ~ '^[0-9]{4,64}$'),
    is_active boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1 CHECK(row_version > 0),
    created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
    PRIMARY KEY(organization_id, product_id),
    UNIQUE(organization_id, operation_id),
    UNIQUE(organization_id, sku)
);
CREATE UNIQUE INDEX products_barcode_unique
    ON catalog.products(organization_id, barcode) WHERE barcode IS NOT NULL;
ALTER TABLE catalog.products ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.products FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON catalog.products
    USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
    WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

CREATE TABLE catalog.product_audit (
    audit_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id uuid NOT NULL,
    product_id uuid NOT NULL,
    action text NOT NULL CHECK(action IN ('INSERT','UPDATE')),
    issuer text NOT NULL,
    subject text NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT statement_timestamp()
);
REVOKE ALL ON catalog.product_audit FROM PUBLIC, salekhpos_runtime;

CREATE FUNCTION catalog.audit_product_mutation() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
BEGIN
  INSERT INTO catalog.product_audit(organization_id,product_id,action,issuer,subject)
  VALUES(NEW.organization_id,NEW.product_id,TG_OP,current_setting('app.issuer'),current_setting('app.subject'));
  RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION catalog.audit_product_mutation() FROM PUBLIC;
CREATE TRIGGER product_audit AFTER INSERT OR UPDATE ON catalog.products
FOR EACH ROW EXECUTE FUNCTION catalog.audit_product_mutation();

GRANT USAGE ON SCHEMA catalog TO salekhpos_runtime;
GRANT SELECT ON catalog.products TO salekhpos_runtime;
GRANT INSERT(organization_id,product_id,operation_id,sku,name,unit_code,barcode) ON catalog.products TO salekhpos_runtime;
GRANT UPDATE(name,unit_code,barcode,is_active,row_version,updated_at) ON catalog.products TO salekhpos_runtime;
COMMIT;
