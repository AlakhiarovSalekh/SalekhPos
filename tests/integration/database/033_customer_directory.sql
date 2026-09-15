BEGIN;
DO $$ BEGIN
 IF NOT EXISTS (SELECT FROM pg_class WHERE oid='customers.customers'::regclass AND relrowsecurity AND relforcerowsecurity)
 THEN RAISE EXCEPTION 'Customer directory must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_policies WHERE schemaname='customers' AND tablename='customers' AND policyname='tenant_isolation')
 THEN RAISE EXCEPTION 'Customer tenant policy is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','customers.customers','SELECT')
    OR has_table_privilege('salekhpos_runtime','customers.customers','DELETE')
 THEN RAISE EXCEPTION 'Customer runtime privileges are unsafe'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','customers.customers','customer_id','INSERT')
    OR NOT has_column_privilege('salekhpos_runtime','customers.customers','display_name','UPDATE')
 THEN RAISE EXCEPTION 'Customer write grants are incomplete'; END IF;
END $$;
ROLLBACK;
