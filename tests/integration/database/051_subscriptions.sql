BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('subscriptions'),('operations'),('entitlement_snapshots')) v(t)
   LEFT JOIN pg_class c ON c.oid=('subscriptions.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Subscription tenant tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='subscriptions'
   AND indexname='ux_subscriptions_current')
 THEN RAISE EXCEPTION 'Current-subscription uniqueness index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','subscriptions.plans','SELECT')
   OR has_table_privilege('salekhpos_runtime','subscriptions.plans','INSERT')
 THEN RAISE EXCEPTION 'Subscription plan runtime grants are unsafe'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','subscriptions.subscriptions','status','UPDATE')
   OR has_column_privilege('salekhpos_runtime','subscriptions.subscriptions','request_hash','UPDATE')
   OR has_table_privilege('salekhpos_runtime','subscriptions.subscriptions','DELETE')
 THEN RAISE EXCEPTION 'Subscription mutation grants are unsafe'; END IF;
END $$;
ROLLBACK;
