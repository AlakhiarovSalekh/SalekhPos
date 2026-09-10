DO $$ BEGIN
 IF NOT EXISTS(SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE n.nspname='sync' AND c.relname='message_results' AND c.relrowsecurity AND c.relforcerowsecurity)
   THEN RAISE EXCEPTION 'Sync result forced RLS missing'; END IF;
 IF has_table_privilege('salekhpos_runtime','sync.message_results','UPDATE')
   OR has_table_privilege('salekhpos_runtime','sync.message_results','DELETE')
   OR has_table_privilege('salekhpos_runtime','sync.message_results','TRUNCATE')
   THEN RAISE EXCEPTION 'Runtime may mutate sync result evidence'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','sync.message_results','status','INSERT')
   OR NOT has_table_privilege('salekhpos_runtime','sync.message_results','SELECT')
   THEN RAISE EXCEPTION 'Runtime sync result admission/read privileges missing'; END IF;
END $$;
