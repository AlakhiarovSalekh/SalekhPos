DO $$ BEGIN
IF NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='sales' AND c.relname='sale_voids' AND c.relrowsecurity AND c.relforcerowsecurity) THEN RAISE EXCEPTION 'sale void isolation missing'; END IF;
IF has_table_privilege('salekhpos_runtime','sales.sale_voids','UPDATE') OR has_table_privilege('salekhpos_runtime','payments.void_refunds','INSERT') THEN RAISE EXCEPTION 'unsafe void privileges'; END IF;
END $$;
