BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('organization_settings'),('operations')) v(t)
   LEFT JOIN pg_class c ON c.oid=('localization.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Localization tables must force RLS'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','localization.operations','SELECT')
   OR has_table_privilege('salekhpos_runtime','localization.operations','UPDATE')
   OR has_table_privilege('salekhpos_runtime','localization.operations','DELETE')
 THEN RAISE EXCEPTION 'Localization replay ledger must remain immutable'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','localization.organization_settings','row_version','UPDATE')
   OR NOT has_column_privilege('salekhpos_runtime','localization.organization_settings','time_zone','UPDATE')
 THEN RAISE EXCEPTION 'Localization update grants are incomplete'; END IF;
 IF has_table_privilege('salekhpos_runtime','localization.organization_settings','DELETE')
 THEN RAISE EXCEPTION 'Localization settings must not be deletable at runtime'; END IF;
END $$;
ROLLBACK;
