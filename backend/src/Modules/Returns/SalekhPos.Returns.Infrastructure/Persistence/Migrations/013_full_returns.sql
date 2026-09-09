BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE SCHEMA returns;
REVOKE ALL ON SCHEMA returns FROM PUBLIC;
CREATE TABLE returns.completed_returns(
 organization_id uuid NOT NULL, return_id uuid NOT NULL, operation_id uuid NOT NULL, sale_id uuid NOT NULL,
 branch_id uuid NOT NULL, currency char(3) NOT NULL, amount numeric(20,6) NOT NULL CHECK(amount>0),
 reason text NOT NULL CHECK(char_length(reason) BETWEEN 3 AND 500), completed_at timestamptz NOT NULL,
 issuer text NOT NULL CHECK(char_length(issuer) BETWEEN 1 AND 2048), subject text NOT NULL CHECK(char_length(subject) BETWEEN 1 AND 256),
 PRIMARY KEY(organization_id,return_id), UNIQUE(organization_id,operation_id), UNIQUE(organization_id,sale_id),
 FOREIGN KEY(organization_id,sale_id) REFERENCES sales.completed_sales(organization_id,sale_id),
 FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id));
CREATE TABLE returns.return_lines(
 organization_id uuid NOT NULL, return_id uuid NOT NULL, line_number integer NOT NULL, product_id uuid NOT NULL,
 quantity numeric(20,6) NOT NULL CHECK(quantity>0), amount numeric(20,6) NOT NULL CHECK(amount>0),
 inventory_movement_id uuid NOT NULL, inventory_operation_id uuid NOT NULL,
 PRIMARY KEY(organization_id,return_id,line_number),
 FOREIGN KEY(organization_id,return_id) REFERENCES returns.completed_returns(organization_id,return_id),
 FOREIGN KEY(organization_id,product_id) REFERENCES catalog.products(organization_id,product_id),
 FOREIGN KEY(organization_id,inventory_movement_id) REFERENCES inventory.stock_movements(organization_id,movement_id));
CREATE TABLE payments.refund_records(
 organization_id uuid NOT NULL, refund_id uuid NOT NULL, payment_id uuid NOT NULL, return_id uuid NOT NULL,
 branch_id uuid NOT NULL, method text NOT NULL CHECK(method='cash'), status text NOT NULL CHECK(status='completed'),
 currency char(3) NOT NULL, amount numeric(20,6) NOT NULL CHECK(amount>0), completed_at timestamptz NOT NULL,
 PRIMARY KEY(organization_id,refund_id), UNIQUE(organization_id,return_id),
 FOREIGN KEY(organization_id,payment_id) REFERENCES payments.payment_records(organization_id,payment_id),
 FOREIGN KEY(organization_id,return_id) REFERENCES returns.completed_returns(organization_id,return_id));
ALTER TABLE returns.completed_returns ENABLE ROW LEVEL SECURITY; ALTER TABLE returns.completed_returns FORCE ROW LEVEL SECURITY;
ALTER TABLE returns.return_lines ENABLE ROW LEVEL SECURITY; ALTER TABLE returns.return_lines FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON returns.completed_returns USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON returns.return_lines USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
ALTER TABLE payments.refund_records ENABLE ROW LEVEL SECURITY; ALTER TABLE payments.refund_records FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON payments.refund_records USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE FUNCTION payments.capture_cash_refund() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,payments AS $$
BEGIN
 INSERT INTO payments.refund_records(organization_id,refund_id,payment_id,return_id,branch_id,method,status,currency,amount,completed_at)
 SELECT NEW.organization_id,NEW.return_id,p.payment_id,NEW.return_id,NEW.branch_id,'cash','completed',NEW.currency,NEW.amount,NEW.completed_at
 FROM payments.payment_records p WHERE p.organization_id=NEW.organization_id AND p.sale_id=NEW.sale_id;
 IF NOT FOUND THEN RAISE EXCEPTION 'Original payment is unavailable'; END IF;
 RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION payments.capture_cash_refund() FROM PUBLIC;
CREATE TRIGGER capture_cash_refund AFTER INSERT ON returns.completed_returns FOR EACH ROW EXECUTE FUNCTION payments.capture_cash_refund();
GRANT USAGE ON SCHEMA returns TO salekhpos_runtime;
GRANT SELECT ON returns.completed_returns,returns.return_lines TO salekhpos_runtime;
GRANT INSERT(organization_id,return_id,operation_id,sale_id,branch_id,currency,amount,reason,completed_at,issuer,subject) ON returns.completed_returns TO salekhpos_runtime;
GRANT INSERT(organization_id,return_id,line_number,product_id,quantity,amount,inventory_movement_id,inventory_operation_id) ON returns.return_lines TO salekhpos_runtime;
GRANT SELECT ON payments.refund_records TO salekhpos_runtime;
COMMIT;
