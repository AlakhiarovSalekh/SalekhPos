BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('accounts'),('point_events')) v(t)
   LEFT JOIN pg_class c ON c.oid=('loyalty.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Loyalty tables must force RLS'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','loyalty.accounts','SELECT')
    OR NOT has_table_privilege('salekhpos_runtime','loyalty.point_events','SELECT')
    OR has_table_privilege('salekhpos_runtime','loyalty.point_events','UPDATE')
    OR has_table_privilege('salekhpos_runtime','loyalty.point_events','DELETE')
 THEN RAISE EXCEPTION 'Loyalty event ledger must remain immutable'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','loyalty.accounts','account_id','INSERT')
    OR NOT has_column_privilege('salekhpos_runtime','loyalty.accounts','points_balance','UPDATE')
    OR NOT has_column_privilege('salekhpos_runtime','loyalty.point_events','balance_after','INSERT')
 THEN RAISE EXCEPTION 'Loyalty write grants are incomplete'; END IF;
END $$;
ROLLBACK;
