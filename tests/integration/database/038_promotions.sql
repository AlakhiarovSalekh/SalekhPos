BEGIN;
DO $$ BEGIN
 IF NOT EXISTS (SELECT FROM pg_class WHERE oid='promotions.promotions'::regclass AND relrowsecurity AND relforcerowsecurity)
 THEN RAISE EXCEPTION 'Promotions table must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='promotions' AND indexname='promotions_resolve')
 THEN RAISE EXCEPTION 'Promotion resolver index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','promotions.promotions','SELECT')
    OR has_table_privilege('salekhpos_runtime','promotions.promotions','DELETE')
 THEN RAISE EXCEPTION 'Promotion runtime privileges are unsafe'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','promotions.promotions','promotion_id','INSERT')
    OR NOT has_column_privilege('salekhpos_runtime','promotions.promotions','is_active','UPDATE')
 THEN RAISE EXCEPTION 'Promotion write grants are incomplete'; END IF;
END $$;
ROLLBACK;
