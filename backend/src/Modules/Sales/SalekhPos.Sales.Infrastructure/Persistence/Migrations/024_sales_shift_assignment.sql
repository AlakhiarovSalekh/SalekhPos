BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
ALTER TABLE sales.completed_sales ADD COLUMN shift_id uuid NULL, ADD COLUMN register_id uuid NULL,
 ADD CONSTRAINT ck_completed_sales_shift_assignment CHECK((shift_id IS NULL)=(register_id IS NULL)),
 ADD CONSTRAINT fk_completed_sales_shift FOREIGN KEY(organization_id,branch_id,shift_id) REFERENCES shifts.shifts(organization_id,branch_id,shift_id),
 ADD CONSTRAINT fk_completed_sales_register FOREIGN KEY(organization_id,branch_id,register_id) REFERENCES stores.registers(organization_id,branch_id,register_id);
CREATE INDEX ix_completed_sales_shift ON sales.completed_sales(organization_id,branch_id,shift_id,completed_at,sale_id) WHERE shift_id IS NOT NULL;
GRANT INSERT(shift_id,register_id) ON sales.completed_sales TO salekhpos_runtime;
COMMIT;
