BEGIN;
DO $$ BEGIN
 IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
END $$;
CREATE SCHEMA payments;
REVOKE ALL ON SCHEMA payments FROM PUBLIC;
CREATE TABLE payments.payment_records(
 organization_id uuid NOT NULL,
 payment_id uuid NOT NULL,
 sale_id uuid NOT NULL,
 branch_id uuid NOT NULL,
 method text NOT NULL CHECK(method='cash'),
 status text NOT NULL CHECK(status='completed'),
 currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
 amount numeric(20,6) NOT NULL CHECK(amount>0),
 tendered numeric(20,6) NOT NULL CHECK(tendered>=amount),
 change_amount numeric(20,6) NOT NULL CHECK(change_amount=tendered-amount),
 completed_at timestamptz NOT NULL,
 PRIMARY KEY(organization_id,payment_id),
 UNIQUE(organization_id,sale_id),
 FOREIGN KEY(organization_id,sale_id) REFERENCES sales.completed_sales(organization_id,sale_id),
 FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id)
);
ALTER TABLE payments.payment_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE payments.payment_records FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON payments.payment_records
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE FUNCTION payments.capture_cash_payment() RETURNS trigger
 LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,payments AS $$
BEGIN
 INSERT INTO payments.payment_records(organization_id,payment_id,sale_id,branch_id,method,status,
   currency,amount,tendered,change_amount,completed_at)
 VALUES(NEW.organization_id,NEW.sale_id,NEW.sale_id,NEW.branch_id,'cash','completed',NEW.currency,
   NEW.grand_total,NEW.cash_received,NEW.change_due,NEW.completed_at);
 RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION payments.capture_cash_payment() FROM PUBLIC;
CREATE TRIGGER capture_cash_payment AFTER INSERT ON sales.completed_sales
 FOR EACH ROW EXECUTE FUNCTION payments.capture_cash_payment();
GRANT USAGE ON SCHEMA payments TO salekhpos_runtime;
GRANT SELECT ON payments.payment_records TO salekhpos_runtime;
COMMIT;
