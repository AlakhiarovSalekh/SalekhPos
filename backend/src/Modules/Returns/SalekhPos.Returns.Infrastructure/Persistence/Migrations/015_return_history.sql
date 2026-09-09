BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE INDEX completed_returns_branch_page ON returns.completed_returns(organization_id,branch_id,return_id);
COMMIT;
