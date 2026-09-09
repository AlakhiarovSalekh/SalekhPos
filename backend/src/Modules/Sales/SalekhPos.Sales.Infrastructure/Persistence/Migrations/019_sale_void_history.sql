BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE INDEX ix_sale_voids_tenant_branch_cursor
 ON sales.sale_voids(organization_id,branch_id,void_id);
COMMIT;
