DO $$ BEGIN
 IF NOT EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='shifts' AND c.relname='cash_movements' AND c.relrowsecurity AND c.relforcerowsecurity) THEN RAISE EXCEPTION 'cash movement RLS missing'; END IF;
 IF has_table_privilege('salekhpos_runtime','shifts.cash_movements','UPDATE') OR has_table_privilege('salekhpos_runtime','shifts.cash_movements','DELETE') THEN RAISE EXCEPTION 'cash movements must be immutable'; END IF;
END $$;
