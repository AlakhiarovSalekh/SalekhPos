BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('documents'),('submission_attempts')) v(t)
   LEFT JOIN pg_class c ON c.oid=('fiscalization.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Fiscalization tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='fiscalization'
   AND indexname='fiscal_documents_retry')
 THEN RAISE EXCEPTION 'Fiscal retry index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','fiscalization.documents','SELECT')
   OR NOT has_table_privilege('salekhpos_runtime','fiscalization.submission_attempts','INSERT')
 THEN RAISE EXCEPTION 'Fiscal runtime grants are incomplete'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','fiscalization.documents','status','UPDATE')
   OR has_column_privilege('salekhpos_runtime','fiscalization.documents','payload','UPDATE')
   OR has_table_privilege('salekhpos_runtime','fiscalization.documents','DELETE')
 THEN RAISE EXCEPTION 'Fiscal document mutation grants are unsafe'; END IF;
END $$;
ROLLBACK;
