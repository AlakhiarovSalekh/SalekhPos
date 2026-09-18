BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('organization_entitlements'),('organization_overrides')) v(t)
   LEFT JOIN pg_class c ON c.oid=('feature_management.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Feature-management tenant tables must force RLS'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','feature_management.features','SELECT')
   OR has_table_privilege('salekhpos_runtime','feature_management.features','INSERT')
 THEN RAISE EXCEPTION 'Feature catalog runtime grants are unsafe'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','feature_management.organization_entitlements','SELECT')
   OR has_table_privilege('salekhpos_runtime','feature_management.organization_entitlements','UPDATE')
 THEN RAISE EXCEPTION 'Entitlements must remain runtime read-only'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','feature_management.organization_overrides','enabled','UPDATE')
   OR has_table_privilege('salekhpos_runtime','feature_management.organization_overrides','DELETE')
 THEN RAISE EXCEPTION 'Feature override mutation grants are unsafe'; END IF;
END $$;
ROLLBACK;
