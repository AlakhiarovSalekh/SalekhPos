DO $$ BEGIN
IF NOT EXISTS(
 SELECT 1 FROM pg_indexes WHERE schemaname='sales' AND tablename='sale_voids'
 AND indexname='ix_sale_voids_tenant_branch_cursor')
THEN RAISE EXCEPTION 'sale void history index missing'; END IF;
END $$;
