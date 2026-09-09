BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
ALTER TABLE returns.completed_returns ADD COLUMN shift_id uuid NULL, ADD COLUMN register_id uuid NULL,
 ADD CONSTRAINT ck_returns_shift_assignment CHECK((shift_id IS NULL)=(register_id IS NULL)),
 ADD CONSTRAINT fk_returns_shift FOREIGN KEY(organization_id,branch_id,shift_id) REFERENCES shifts.shifts(organization_id,branch_id,shift_id),
 ADD CONSTRAINT fk_returns_register FOREIGN KEY(organization_id,branch_id,register_id) REFERENCES stores.registers(organization_id,branch_id,register_id);
ALTER TABLE sales.sale_voids ADD COLUMN shift_id uuid NULL, ADD COLUMN register_id uuid NULL,
 ADD CONSTRAINT ck_voids_shift_assignment CHECK((shift_id IS NULL)=(register_id IS NULL)),
 ADD CONSTRAINT fk_voids_shift FOREIGN KEY(organization_id,branch_id,shift_id) REFERENCES shifts.shifts(organization_id,branch_id,shift_id),
 ADD CONSTRAINT fk_voids_register FOREIGN KEY(organization_id,branch_id,register_id) REFERENCES stores.registers(organization_id,branch_id,register_id);
ALTER TABLE payments.refund_records ADD COLUMN shift_id uuid NULL, ADD COLUMN register_id uuid NULL;
ALTER TABLE payments.void_refunds ADD COLUMN shift_id uuid NULL, ADD COLUMN register_id uuid NULL;
ALTER FUNCTION payments.capture_cash_refund() RENAME TO capture_cash_refund_legacy;
CREATE FUNCTION payments.capture_cash_refund() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,payments AS $$ BEGIN
 INSERT INTO payments.refund_records(organization_id,refund_id,payment_id,return_id,branch_id,method,status,currency,amount,completed_at,shift_id,register_id)
 SELECT NEW.organization_id,NEW.return_id,p.payment_id,NEW.return_id,NEW.branch_id,'cash','completed',NEW.currency,NEW.amount,NEW.completed_at,NEW.shift_id,NEW.register_id FROM payments.payment_records p WHERE p.organization_id=NEW.organization_id AND p.sale_id=NEW.sale_id;
 IF NOT FOUND THEN RAISE EXCEPTION 'Original payment is unavailable'; END IF; RETURN NEW; END $$;
DROP TRIGGER capture_cash_refund ON returns.completed_returns; CREATE TRIGGER capture_cash_refund AFTER INSERT ON returns.completed_returns FOR EACH ROW EXECUTE FUNCTION payments.capture_cash_refund();
DROP FUNCTION payments.capture_cash_refund_legacy(); REVOKE ALL ON FUNCTION payments.capture_cash_refund() FROM PUBLIC;
ALTER FUNCTION payments.capture_void_refund() RENAME TO capture_void_refund_legacy;
CREATE FUNCTION payments.capture_void_refund() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,payments AS $$ BEGIN
 INSERT INTO payments.void_refunds(organization_id,void_id,payment_id,currency,amount,completed_at,shift_id,register_id)
 SELECT NEW.organization_id,NEW.void_id,p.payment_id,NEW.currency,NEW.amount,NEW.voided_at,NEW.shift_id,NEW.register_id FROM payments.payment_records p WHERE p.organization_id=NEW.organization_id AND p.sale_id=NEW.sale_id;
 IF NOT FOUND THEN RAISE EXCEPTION 'Original payment is unavailable'; END IF; RETURN NEW; END $$;
DROP TRIGGER capture_void_refund ON sales.sale_voids; CREATE TRIGGER capture_void_refund AFTER INSERT ON sales.sale_voids FOR EACH ROW EXECUTE FUNCTION payments.capture_void_refund();
DROP FUNCTION payments.capture_void_refund_legacy(); REVOKE ALL ON FUNCTION payments.capture_void_refund() FROM PUBLIC;
ALTER TABLE shifts.shifts ADD COLUMN close_operation_id uuid NULL, ADD COLUMN cash_sales numeric(20,6) NULL, ADD COLUMN cash_refunds numeric(20,6) NULL,
 ADD COLUMN cash_in numeric(20,6) NULL, ADD COLUMN cash_out numeric(20,6) NULL, ADD COLUMN expected_cash numeric(20,6) NULL,
 ADD COLUMN counted_cash numeric(20,6) NULL, ADD COLUMN variance numeric(20,6) NULL, ADD COLUMN closed_by text NULL,
 ADD CONSTRAINT uq_shift_close_operation UNIQUE(organization_id,close_operation_id),
 ADD CONSTRAINT ck_shift_close_evidence CHECK((status='open' AND close_operation_id IS NULL AND closed_at IS NULL AND closed_by IS NULL AND expected_cash IS NULL) OR (status='closed' AND close_operation_id IS NOT NULL AND closed_at IS NOT NULL AND closed_by IS NOT NULL AND cash_sales IS NOT NULL AND cash_refunds IS NOT NULL AND cash_in IS NOT NULL AND cash_out IS NOT NULL AND expected_cash IS NOT NULL AND counted_cash IS NOT NULL AND variance IS NOT NULL));
GRANT INSERT(shift_id,register_id) ON returns.completed_returns,sales.sale_voids TO salekhpos_runtime;
GRANT UPDATE(status,close_operation_id,cash_sales,cash_refunds,cash_in,cash_out,expected_cash,counted_cash,variance,closed_at,closed_by) ON shifts.shifts TO salekhpos_runtime;
COMMIT;
