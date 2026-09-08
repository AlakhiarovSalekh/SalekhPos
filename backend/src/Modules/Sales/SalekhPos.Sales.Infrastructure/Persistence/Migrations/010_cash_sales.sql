BEGIN;
DO $$ BEGIN
 IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
 IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN RAISE EXCEPTION 'Provision runtime role'; END IF;
END $$;
CREATE SCHEMA sales;
REVOKE ALL ON SCHEMA sales FROM PUBLIC;
CREATE TABLE sales.completed_sales(
 organization_id uuid NOT NULL,
 sale_id uuid NOT NULL,
 operation_id uuid NOT NULL,
 branch_id uuid NOT NULL,
 currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
 net_total numeric(20,6) NOT NULL CHECK(net_total>=0),
 tax_total numeric(20,6) NOT NULL CHECK(tax_total>=0),
 grand_total numeric(20,6) NOT NULL CHECK(grand_total>0 AND grand_total=net_total+tax_total),
 cash_received numeric(20,6) NOT NULL CHECK(cash_received>=grand_total),
 change_due numeric(20,6) NOT NULL CHECK(change_due=cash_received-grand_total),
 completed_at timestamptz NOT NULL,
 issuer text NOT NULL CHECK(char_length(issuer) BETWEEN 1 AND 2048),
 subject text NOT NULL CHECK(char_length(subject) BETWEEN 1 AND 256),
 PRIMARY KEY(organization_id,sale_id), UNIQUE(organization_id,operation_id),
 FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id)
);
CREATE TABLE sales.sale_lines(
 organization_id uuid NOT NULL,
 sale_id uuid NOT NULL,
 line_number integer NOT NULL CHECK(line_number BETWEEN 1 AND 500),
 product_id uuid NOT NULL,
 price_id uuid NOT NULL,
 inventory_movement_id uuid NOT NULL,
 inventory_operation_id uuid NOT NULL,
 quantity numeric(20,6) NOT NULL CHECK(quantity>0),
 unit_amount numeric(20,6) NOT NULL CHECK(unit_amount>0),
 currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
 tax_mode text NOT NULL CHECK(tax_mode IN('inclusive','exclusive')),
 tax_rate numeric(7,4) NOT NULL CHECK(tax_rate BETWEEN 0 AND 100),
 net_amount numeric(20,6) NOT NULL CHECK(net_amount>=0),
 tax_amount numeric(20,6) NOT NULL CHECK(tax_amount>=0),
 gross_amount numeric(20,6) NOT NULL CHECK(gross_amount=net_amount+tax_amount),
 PRIMARY KEY(organization_id,sale_id,line_number),
 UNIQUE(organization_id,sale_id,product_id),
 FOREIGN KEY(organization_id,sale_id) REFERENCES sales.completed_sales(organization_id,sale_id),
 FOREIGN KEY(organization_id,product_id) REFERENCES catalog.products(organization_id,product_id),
 FOREIGN KEY(organization_id,price_id) REFERENCES pricing.prices(organization_id,price_id),
 FOREIGN KEY(organization_id,inventory_movement_id) REFERENCES inventory.stock_movements(organization_id,movement_id),
 FOREIGN KEY(organization_id,inventory_operation_id) REFERENCES inventory.stock_movements(organization_id,operation_id)
);
CREATE TABLE sales.outbox_messages(
 organization_id uuid NOT NULL,
 message_id uuid NOT NULL,
 sale_id uuid NOT NULL,
 event_type text NOT NULL CHECK(event_type='sales.sale_completed.v1'),
 payload jsonb NOT NULL,
 occurred_at timestamptz NOT NULL,
 dispatched_at timestamptz,
 PRIMARY KEY(organization_id,message_id),
 FOREIGN KEY(organization_id,sale_id) REFERENCES sales.completed_sales(organization_id,sale_id)
);
ALTER TABLE sales.completed_sales ENABLE ROW LEVEL SECURITY; ALTER TABLE sales.completed_sales FORCE ROW LEVEL SECURITY;
ALTER TABLE sales.sale_lines ENABLE ROW LEVEL SECURITY; ALTER TABLE sales.sale_lines FORCE ROW LEVEL SECURITY;
ALTER TABLE sales.outbox_messages ENABLE ROW LEVEL SECURITY; ALTER TABLE sales.outbox_messages FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON sales.completed_sales USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON sales.sale_lines USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON sales.outbox_messages USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA sales TO salekhpos_runtime;
GRANT SELECT ON sales.completed_sales,sales.sale_lines TO salekhpos_runtime;
GRANT INSERT(organization_id,sale_id,operation_id,branch_id,currency,net_total,tax_total,grand_total,cash_received,change_due,completed_at,issuer,subject) ON sales.completed_sales TO salekhpos_runtime;
GRANT INSERT(organization_id,sale_id,line_number,product_id,price_id,inventory_movement_id,inventory_operation_id,quantity,unit_amount,currency,tax_mode,tax_rate,net_amount,tax_amount,gross_amount) ON sales.sale_lines TO salekhpos_runtime;
GRANT INSERT(organization_id,message_id,sale_id,event_type,payload,occurred_at) ON sales.outbox_messages TO salekhpos_runtime;
COMMIT;
