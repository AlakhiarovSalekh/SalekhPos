BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE SCHEMA IF NOT EXISTS purchasing;
REVOKE ALL ON SCHEMA purchasing FROM PUBLIC;

CREATE TABLE purchasing.purchase_orders(
  organization_id uuid NOT NULL,
  order_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  branch_id uuid NOT NULL,
  supplier_id uuid NOT NULL,
  status varchar(16) NOT NULL DEFAULT 'draft',
  currency char(3) NOT NULL,
  reference varchar(100),
  total numeric(30,6) NOT NULL,
  row_version bigint NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL,
  updated_at timestamptz NOT NULL,
  issuer text NOT NULL,
  subject text NOT NULL,
  PRIMARY KEY(organization_id,order_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id),
  FOREIGN KEY(organization_id,supplier_id) REFERENCES suppliers.suppliers(organization_id,supplier_id),
  CHECK(status IN ('draft','submitted','approved','cancelled')),
  CHECK(currency ~ '^[A-Z]{3}$'),
  CHECK(reference IS NULL OR (char_length(reference) BETWEEN 1 AND 100 AND reference=btrim(reference))),
  CHECK(total >= 0), CHECK(row_version >= 1)
);
CREATE INDEX ix_purchase_orders_branch_cursor ON purchasing.purchase_orders(organization_id,branch_id,order_id);
CREATE INDEX ix_purchase_orders_supplier ON purchasing.purchase_orders(organization_id,supplier_id,status);

CREATE TABLE purchasing.purchase_order_lines(
  organization_id uuid NOT NULL,
  order_id uuid NOT NULL,
  line_number integer NOT NULL,
  product_id uuid NOT NULL,
  quantity numeric(24,6) NOT NULL,
  unit_cost numeric(24,6) NOT NULL,
  line_total numeric(30,6) NOT NULL,
  PRIMARY KEY(organization_id,order_id,line_number),
  UNIQUE(organization_id,order_id,product_id),
  FOREIGN KEY(organization_id,order_id) REFERENCES purchasing.purchase_orders(organization_id,order_id) ON DELETE RESTRICT,
  FOREIGN KEY(organization_id,product_id) REFERENCES catalog.products(organization_id,product_id),
  CHECK(line_number BETWEEN 1 AND 500), CHECK(quantity > 0), CHECK(unit_cost >= 0),
  CHECK(line_total = quantity * unit_cost)
);
ALTER TABLE purchasing.purchase_orders ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.purchase_orders FORCE ROW LEVEL SECURITY;
ALTER TABLE purchasing.purchase_order_lines ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.purchase_order_lines FORCE ROW LEVEL SECURITY;
CREATE POLICY purchase_orders_tenant ON purchasing.purchase_orders
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY purchase_order_lines_tenant ON purchasing.purchase_order_lines
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

GRANT USAGE ON SCHEMA purchasing TO salekhpos_runtime;
GRANT SELECT ON purchasing.purchase_orders,purchasing.purchase_order_lines TO salekhpos_runtime;
GRANT INSERT(organization_id,order_id,operation_id,branch_id,supplier_id,status,currency,reference,total,
  row_version,created_at,updated_at,issuer,subject) ON purchasing.purchase_orders TO salekhpos_runtime;
GRANT UPDATE(status,row_version,updated_at) ON purchasing.purchase_orders TO salekhpos_runtime;
GRANT INSERT(organization_id,order_id,line_number,product_id,quantity,unit_cost,line_total)
  ON purchasing.purchase_order_lines TO salekhpos_runtime;
REVOKE DELETE ON purchasing.purchase_orders,purchasing.purchase_order_lines FROM salekhpos_runtime;
COMMIT;
