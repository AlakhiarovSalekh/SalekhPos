BEGIN;
DO $$ BEGIN
 IF NOT EXISTS (SELECT FROM pg_class WHERE oid='suppliers.suppliers'::regclass AND relrowsecurity AND relforcerowsecurity)
 THEN RAISE EXCEPTION 'Supplier directory must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_policies WHERE schemaname='suppliers' AND tablename='suppliers' AND policyname='tenant_isolation')
 THEN RAISE EXCEPTION 'Supplier tenant policy is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','suppliers.suppliers','SELECT')
    OR has_table_privilege('salekhpos_runtime','suppliers.suppliers','DELETE')
 THEN RAISE EXCEPTION 'Supplier runtime privileges are unsafe'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','suppliers.suppliers','supplier_id','INSERT')
    OR NOT has_column_privilege('salekhpos_runtime','suppliers.suppliers','name','UPDATE')
 THEN RAISE EXCEPTION 'Supplier write grants are incomplete'; END IF;
END $$;
ROLLBACK;
