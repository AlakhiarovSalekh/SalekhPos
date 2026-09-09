BEGIN;
DO $$ BEGIN
 IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
END $$;
CREATE INDEX completed_sales_branch_page
 ON sales.completed_sales(organization_id,branch_id,sale_id);
COMMIT;
