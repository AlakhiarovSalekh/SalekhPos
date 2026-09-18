BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('tickets'),('ticket_transitions'),('diagnostic_references')) v(t)
   LEFT JOIN pg_class c ON c.oid=('support.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Support tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='support'
   AND indexname='ix_support_tickets_status')
 THEN RAISE EXCEPTION 'Support status index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','support.tickets','INSERT')
   OR NOT has_table_privilege('salekhpos_runtime','support.diagnostic_references','SELECT')
 THEN RAISE EXCEPTION 'Support runtime grants are incomplete'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','support.tickets','status','UPDATE')
   OR has_column_privilege('salekhpos_runtime','support.tickets','description','UPDATE')
   OR has_table_privilege('salekhpos_runtime','support.tickets','DELETE')
 THEN RAISE EXCEPTION 'Support ticket mutation grants are unsafe'; END IF;
END $$;
ROLLBACK;
