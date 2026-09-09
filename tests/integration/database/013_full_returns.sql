BEGIN;
DO $$ BEGIN
 IF NOT EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE n.nspname='returns' AND c.relname='completed_returns' AND c.relrowsecurity AND c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'returns isolation missing'; END IF;
 IF has_table_privilege('salekhpos_runtime','returns.completed_returns','UPDATE')
   OR has_table_privilege('salekhpos_runtime','returns.completed_returns','DELETE')
 THEN RAISE EXCEPTION 'runtime can mutate returns'; END IF;
END $$;
ROLLBACK;
