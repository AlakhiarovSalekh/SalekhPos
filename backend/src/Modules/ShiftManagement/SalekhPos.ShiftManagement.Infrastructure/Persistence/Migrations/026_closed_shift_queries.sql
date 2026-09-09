BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE INDEX ix_shifts_closed_branch_cursor
    ON shifts.shifts(organization_id,branch_id,shift_id DESC)
    WHERE status='closed';
COMMIT;
