BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('stream_heads'),('events')) v(t)
   LEFT JOIN pg_class c ON c.oid=('audit.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Audit tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='audit' AND indexname='audit_events_sequence')
 THEN RAISE EXCEPTION 'Audit sequence index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','audit.events','SELECT')
    OR has_table_privilege('salekhpos_runtime','audit.events','UPDATE')
    OR has_table_privilege('salekhpos_runtime','audit.events','DELETE')
 THEN RAISE EXCEPTION 'Audit event ledger must remain immutable'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','audit.events','event_hash','INSERT')
    OR NOT has_column_privilege('salekhpos_runtime','audit.stream_heads','last_hash','UPDATE')
 THEN RAISE EXCEPTION 'Audit append grants are incomplete'; END IF;
END $$;
ROLLBACK;
