BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('tax_profiles'),('tax_rates')) v(t)
   LEFT JOIN pg_class c ON c.oid=('taxation.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Taxation tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='taxation'
   AND indexname='ix_tax_rates_resolve')
 THEN RAISE EXCEPTION 'Tax rate resolution index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','taxation.tax_profiles','SELECT')
   OR NOT has_table_privilege('salekhpos_runtime','taxation.tax_rates','SELECT')
 THEN RAISE EXCEPTION 'Taxation read grants are incomplete'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','taxation.tax_rates','is_active','UPDATE')
   OR has_column_privilege('salekhpos_runtime','taxation.tax_rates','rate_percent','UPDATE')
 THEN RAISE EXCEPTION 'Tax rate mutation grants are unsafe'; END IF;
END $$;
ROLLBACK;
