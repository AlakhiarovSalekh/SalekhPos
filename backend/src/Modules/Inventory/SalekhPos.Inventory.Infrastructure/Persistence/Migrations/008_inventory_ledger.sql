BEGIN;
DO $$ BEGIN
  IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
  IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN RAISE EXCEPTION 'Provision runtime role'; END IF;
END $$;
CREATE SCHEMA inventory;
REVOKE ALL ON SCHEMA inventory FROM PUBLIC;
CREATE TABLE inventory.stock_movements(
  organization_id uuid NOT NULL,
  movement_id uuid NOT NULL CHECK(movement_id<>'00000000-0000-0000-0000-000000000000'),
  operation_id uuid NOT NULL CHECK(operation_id<>'00000000-0000-0000-0000-000000000000'),
  branch_id uuid NOT NULL,
  product_id uuid NOT NULL,
  kind text NOT NULL CHECK(kind IN('receipt','adjustment_in','adjustment_out','sale','return')),
  direction smallint NOT NULL CHECK(direction IN(-1,1)),
  quantity numeric(20,6) NOT NULL CHECK(quantity>0),
  reason text CHECK(reason=btrim(reason) AND char_length(reason) BETWEEN 1 AND 200),
  occurred_at timestamptz NOT NULL,
  recorded_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  issuer text NOT NULL CHECK(char_length(issuer) BETWEEN 1 AND 2048),
  subject text NOT NULL CHECK(char_length(subject) BETWEEN 1 AND 256),
  PRIMARY KEY(organization_id,movement_id), UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id),
  FOREIGN KEY(organization_id,product_id) REFERENCES catalog.products(organization_id,product_id),
  CHECK((kind IN('adjustment_out','sale') AND direction=-1) OR (kind IN('receipt','adjustment_in','return') AND direction=1))
);
CREATE INDEX stock_movements_balance ON inventory.stock_movements(organization_id,branch_id,product_id);
ALTER TABLE inventory.stock_movements ENABLE ROW LEVEL SECURITY;
ALTER TABLE inventory.stock_movements FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON inventory.stock_movements
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA inventory TO salekhpos_runtime;
GRANT SELECT ON inventory.stock_movements TO salekhpos_runtime;
GRANT INSERT(organization_id,movement_id,operation_id,branch_id,product_id,kind,direction,quantity,reason,occurred_at,issuer,subject)
 ON inventory.stock_movements TO salekhpos_runtime;
COMMIT;
