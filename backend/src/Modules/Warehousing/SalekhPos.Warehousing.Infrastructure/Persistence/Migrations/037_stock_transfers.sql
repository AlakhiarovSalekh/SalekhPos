BEGIN;
DO $$ BEGIN
  IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
  IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN RAISE EXCEPTION 'Provision runtime role'; END IF;
END $$;

CREATE SCHEMA warehousing;
REVOKE ALL ON SCHEMA warehousing FROM PUBLIC;
CREATE TABLE warehousing.stock_transfers(
  organization_id uuid NOT NULL,
  transfer_id uuid NOT NULL CHECK(transfer_id<>'00000000-0000-0000-0000-000000000000'),
  operation_id uuid NOT NULL CHECK(operation_id<>'00000000-0000-0000-0000-000000000000'),
  source_branch_id uuid NOT NULL,
  destination_branch_id uuid NOT NULL,
  status text NOT NULL CHECK(status IN('draft','in_transit','received','cancelled')),
  reference text CHECK(reference=btrim(reference) AND char_length(reference) BETWEEN 1 AND 120),
  row_version bigint NOT NULL DEFAULT 1 CHECK(row_version>0),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  dispatched_at timestamptz,
  received_at timestamptz,
  issuer text NOT NULL CHECK(char_length(issuer) BETWEEN 1 AND 2048),
  subject text NOT NULL CHECK(char_length(subject) BETWEEN 1 AND 256),  PRIMARY KEY(organization_id,transfer_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,source_branch_id) REFERENCES organization.branches(organization_id,branch_id),
  FOREIGN KEY(organization_id,destination_branch_id) REFERENCES organization.branches(organization_id,branch_id),
  CHECK(source_branch_id<>destination_branch_id),
  CHECK((status='draft' AND dispatched_at IS NULL AND received_at IS NULL)
     OR (status='in_transit' AND dispatched_at IS NOT NULL AND received_at IS NULL)
     OR (status='received' AND dispatched_at IS NOT NULL AND received_at IS NOT NULL)
     OR (status='cancelled' AND dispatched_at IS NULL AND received_at IS NULL))
);
CREATE INDEX stock_transfers_branch_lookup
  ON warehousing.stock_transfers(organization_id,source_branch_id,destination_branch_id,transfer_id);

CREATE TABLE warehousing.stock_transfer_lines(
  organization_id uuid NOT NULL,
  transfer_id uuid NOT NULL,
  line_no integer NOT NULL CHECK(line_no BETWEEN 1 AND 500),
  product_id uuid NOT NULL,
  quantity numeric(20,6) NOT NULL CHECK(quantity>0),
  dispatch_operation_id uuid NOT NULL CHECK(dispatch_operation_id<>'00000000-0000-0000-0000-000000000000'),
  receive_operation_id uuid NOT NULL CHECK(receive_operation_id<>'00000000-0000-0000-0000-000000000000'),
  PRIMARY KEY(organization_id,transfer_id,line_no),
  UNIQUE(organization_id,transfer_id,product_id),
  UNIQUE(organization_id,dispatch_operation_id),
  UNIQUE(organization_id,receive_operation_id),
  FOREIGN KEY(organization_id,transfer_id) REFERENCES warehousing.stock_transfers(organization_id,transfer_id) ON DELETE CASCADE,
  FOREIGN KEY(organization_id,product_id) REFERENCES catalog.products(organization_id,product_id)
);
ALTER TABLE inventory.stock_movements DROP CONSTRAINT IF EXISTS stock_movements_kind_check;
ALTER TABLE inventory.stock_movements DROP CONSTRAINT IF EXISTS stock_movements_check;
ALTER TABLE inventory.stock_movements DROP CONSTRAINT IF EXISTS stock_movements_direction_semantics_check;
ALTER TABLE inventory.stock_movements ADD CONSTRAINT stock_movements_kind_check
  CHECK(kind IN('receipt','adjustment_in','adjustment_out','sale','return','transfer_out','transfer_in'));
ALTER TABLE inventory.stock_movements ADD CONSTRAINT stock_movements_direction_semantics_check CHECK(
  (kind IN('adjustment_out','sale','transfer_out') AND direction=-1)
  OR (kind IN('receipt','adjustment_in','return','transfer_in') AND direction=1));

ALTER TABLE warehousing.stock_transfers ENABLE ROW LEVEL SECURITY;
ALTER TABLE warehousing.stock_transfers FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON warehousing.stock_transfers
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
ALTER TABLE warehousing.stock_transfer_lines ENABLE ROW LEVEL SECURITY;
ALTER TABLE warehousing.stock_transfer_lines FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON warehousing.stock_transfer_lines
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);

GRANT USAGE ON SCHEMA warehousing TO salekhpos_runtime;
GRANT SELECT ON warehousing.stock_transfers,warehousing.stock_transfer_lines TO salekhpos_runtime;
GRANT INSERT(organization_id,transfer_id,operation_id,source_branch_id,
  destination_branch_id,status,reference,issuer,subject) ON warehousing.stock_transfers TO salekhpos_runtime;
GRANT INSERT(organization_id,transfer_id,line_no,product_id,quantity,dispatch_operation_id,receive_operation_id)
  ON warehousing.stock_transfer_lines TO salekhpos_runtime;
GRANT UPDATE(status,row_version,updated_at,dispatched_at,received_at)
  ON warehousing.stock_transfers TO salekhpos_runtime;
COMMIT;
