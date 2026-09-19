BEGIN;

ALTER TABLE purchasing.purchase_orders
  DROP CONSTRAINT purchase_orders_status_check;
ALTER TABLE purchasing.purchase_orders
  ALTER COLUMN status TYPE varchar(24);
ALTER TABLE purchasing.purchase_orders
  ADD CONSTRAINT purchase_orders_status_check
  CHECK(status IN ('draft','submitted','approved','partially_received','received','cancelled'));

CREATE TABLE purchasing.purchase_receipts(
  organization_id uuid NOT NULL,
  receipt_id uuid NOT NULL CHECK(receipt_id<>'00000000-0000-0000-0000-000000000000'),
  operation_id uuid NOT NULL CHECK(operation_id<>'00000000-0000-0000-0000-000000000000'),
  order_id uuid NOT NULL,
  branch_id uuid NOT NULL,
  expected_order_version bigint NOT NULL CHECK(expected_order_version>=1),
  reference varchar(120),
  received_at timestamptz NOT NULL,
  received_by_issuer text NOT NULL CHECK(char_length(received_by_issuer) BETWEEN 1 AND 2048),
  received_by_subject text NOT NULL CHECK(char_length(received_by_subject) BETWEEN 1 AND 256),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,receipt_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,order_id)
    REFERENCES purchasing.purchase_orders(organization_id,order_id) ON DELETE RESTRICT,
  FOREIGN KEY(organization_id,branch_id)
    REFERENCES organization.branches(organization_id,branch_id),
  CHECK(reference IS NULL OR (char_length(reference) BETWEEN 1 AND 120 AND reference=btrim(reference)))
);

CREATE INDEX ix_purchase_receipts_order
  ON purchasing.purchase_receipts(organization_id,order_id,created_at,receipt_id);

CREATE TABLE purchasing.purchase_receipt_lines(
  organization_id uuid NOT NULL,
  receipt_id uuid NOT NULL,
  line_number integer NOT NULL CHECK(line_number BETWEEN 1 AND 500),
  order_id uuid NOT NULL,
  product_id uuid NOT NULL,
  quantity numeric(24,6) NOT NULL CHECK(quantity>0),
  movement_id uuid NOT NULL,
  PRIMARY KEY(organization_id,receipt_id,line_number),
  UNIQUE(organization_id,receipt_id,product_id),
  UNIQUE(organization_id,movement_id),
  FOREIGN KEY(organization_id,receipt_id)
    REFERENCES purchasing.purchase_receipts(organization_id,receipt_id) ON DELETE RESTRICT,
  FOREIGN KEY(organization_id,order_id,product_id)
    REFERENCES purchasing.purchase_order_lines(organization_id,order_id,product_id),
  FOREIGN KEY(organization_id,movement_id)
    REFERENCES inventory.stock_movements(organization_id,movement_id)
);

CREATE INDEX ix_purchase_receipt_lines_order_product
  ON purchasing.purchase_receipt_lines(organization_id,order_id,product_id);

ALTER TABLE purchasing.purchase_receipts ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.purchase_receipts FORCE ROW LEVEL SECURITY;
ALTER TABLE purchasing.purchase_receipt_lines ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.purchase_receipt_lines FORCE ROW LEVEL SECURITY;

CREATE POLICY purchase_receipts_tenant ON purchasing.purchase_receipts
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY purchase_receipt_lines_tenant ON purchasing.purchase_receipt_lines
  USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
  WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

GRANT SELECT,INSERT ON purchasing.purchase_receipts,purchasing.purchase_receipt_lines
  TO salekhpos_runtime;
REVOKE UPDATE,DELETE,TRUNCATE ON purchasing.purchase_receipts,purchasing.purchase_receipt_lines
  FROM salekhpos_runtime;

COMMIT;
