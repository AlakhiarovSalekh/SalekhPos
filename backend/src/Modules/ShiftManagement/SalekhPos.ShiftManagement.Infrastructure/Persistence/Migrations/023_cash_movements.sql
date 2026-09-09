BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
ALTER TABLE shifts.shifts ADD CONSTRAINT uq_shifts_tenant_branch_id UNIQUE(organization_id,branch_id,shift_id);
CREATE TABLE shifts.cash_movements(
 organization_id uuid NOT NULL, movement_id uuid NOT NULL, operation_id uuid NOT NULL, shift_id uuid NOT NULL, branch_id uuid NOT NULL,
 kind text NOT NULL CHECK(kind IN('cash_in','cash_out')), currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
 amount numeric(20,6) NOT NULL CHECK(amount>0), reason text NOT NULL CHECK(char_length(reason) BETWEEN 3 AND 500),
 recorded_at timestamptz NOT NULL, issuer text NOT NULL, subject text NOT NULL,
 PRIMARY KEY(organization_id,movement_id), UNIQUE(organization_id,operation_id),
 FOREIGN KEY(organization_id,branch_id,shift_id) REFERENCES shifts.shifts(organization_id,branch_id,shift_id));
CREATE INDEX ix_cash_movements_shift ON shifts.cash_movements(organization_id,branch_id,shift_id,recorded_at,movement_id);
ALTER TABLE shifts.cash_movements ENABLE ROW LEVEL SECURITY; ALTER TABLE shifts.cash_movements FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shifts.cash_movements USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT SELECT ON shifts.cash_movements TO salekhpos_runtime;
GRANT INSERT(organization_id,movement_id,operation_id,shift_id,branch_id,kind,currency,amount,reason,recorded_at,issuer,subject) ON shifts.cash_movements TO salekhpos_runtime;
COMMIT;
