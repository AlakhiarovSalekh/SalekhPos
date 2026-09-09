BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE TABLE sales.sale_voids(
 organization_id uuid NOT NULL, void_id uuid NOT NULL, operation_id uuid NOT NULL, sale_id uuid NOT NULL,
 branch_id uuid NOT NULL, currency char(3) NOT NULL, amount numeric(20,6) NOT NULL CHECK(amount>0),
 reason text NOT NULL CHECK(char_length(reason) BETWEEN 3 AND 500), voided_at timestamptz NOT NULL,
 issuer text NOT NULL, subject text NOT NULL,
 PRIMARY KEY(organization_id,void_id), UNIQUE(organization_id,operation_id), UNIQUE(organization_id,sale_id),
 FOREIGN KEY(organization_id,sale_id) REFERENCES sales.completed_sales(organization_id,sale_id),
 FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id));
CREATE TABLE sales.sale_void_lines(
 organization_id uuid NOT NULL, void_id uuid NOT NULL, line_number integer NOT NULL, product_id uuid NOT NULL,
 quantity numeric(20,6) NOT NULL CHECK(quantity>0), inventory_movement_id uuid NOT NULL,
 PRIMARY KEY(organization_id,void_id,line_number), UNIQUE(organization_id,void_id,product_id),
 FOREIGN KEY(organization_id,void_id) REFERENCES sales.sale_voids(organization_id,void_id),
 FOREIGN KEY(organization_id,inventory_movement_id) REFERENCES inventory.stock_movements(organization_id,movement_id));
CREATE TABLE payments.void_refunds(
 organization_id uuid NOT NULL, void_id uuid NOT NULL, payment_id uuid NOT NULL, currency char(3) NOT NULL,
 amount numeric(20,6) NOT NULL CHECK(amount>0), completed_at timestamptz NOT NULL,
 PRIMARY KEY(organization_id,void_id), FOREIGN KEY(organization_id,void_id) REFERENCES sales.sale_voids(organization_id,void_id),
 FOREIGN KEY(organization_id,payment_id) REFERENCES payments.payment_records(organization_id,payment_id));
ALTER TABLE sales.sale_voids ENABLE ROW LEVEL SECURITY; ALTER TABLE sales.sale_voids FORCE ROW LEVEL SECURITY;
ALTER TABLE sales.sale_void_lines ENABLE ROW LEVEL SECURITY; ALTER TABLE sales.sale_void_lines FORCE ROW LEVEL SECURITY;
ALTER TABLE payments.void_refunds ENABLE ROW LEVEL SECURITY; ALTER TABLE payments.void_refunds FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON sales.sale_voids USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON sales.sale_void_lines USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON payments.void_refunds USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE FUNCTION payments.capture_void_refund() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,payments AS $$ BEGIN
 INSERT INTO payments.void_refunds(organization_id,void_id,payment_id,currency,amount,completed_at)
 SELECT NEW.organization_id,NEW.void_id,p.payment_id,NEW.currency,NEW.amount,NEW.voided_at FROM payments.payment_records p
 WHERE p.organization_id=NEW.organization_id AND p.sale_id=NEW.sale_id;
 IF NOT FOUND THEN RAISE EXCEPTION 'Original payment is unavailable'; END IF; RETURN NEW; END $$;
REVOKE ALL ON FUNCTION payments.capture_void_refund() FROM PUBLIC;
CREATE TRIGGER capture_void_refund AFTER INSERT ON sales.sale_voids FOR EACH ROW EXECUTE FUNCTION payments.capture_void_refund();
GRANT SELECT ON sales.sale_voids,sales.sale_void_lines TO salekhpos_runtime;
GRANT INSERT(organization_id,void_id,operation_id,sale_id,branch_id,currency,amount,reason,voided_at,issuer,subject) ON sales.sale_voids TO salekhpos_runtime;
GRANT INSERT(organization_id,void_id,line_number,product_id,quantity,inventory_movement_id) ON sales.sale_void_lines TO salekhpos_runtime;
GRANT SELECT ON payments.void_refunds TO salekhpos_runtime;
COMMIT;
