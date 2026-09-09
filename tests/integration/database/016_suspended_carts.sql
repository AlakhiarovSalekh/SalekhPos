DO $$ BEGIN
IF NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='sales' AND c.relname='suspended_carts' AND c.relrowsecurity AND c.relforcerowsecurity) THEN RAISE EXCEPTION 'suspended cart isolation missing'; END IF;
IF has_table_privilege('salekhpos_runtime','sales.suspended_carts','DELETE') THEN RAISE EXCEPTION 'runtime can delete carts'; END IF;
END $$;
