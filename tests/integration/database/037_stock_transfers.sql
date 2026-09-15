BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('stock_transfers'),('stock_transfer_lines')) v(t)
   LEFT JOIN pg_class c ON c.oid=('warehousing.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Warehousing tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_constraint WHERE conrelid='inventory.stock_movements'::regclass
   AND pg_get_constraintdef(oid) LIKE '%transfer_out%' AND pg_get_constraintdef(oid) LIKE '%transfer_in%')
 THEN RAISE EXCEPTION 'Inventory transfer movement constraints are missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','warehousing.stock_transfers','SELECT')
    OR has_table_privilege('salekhpos_runtime','warehousing.stock_transfers','DELETE')
 THEN RAISE EXCEPTION 'Warehousing runtime privileges are unsafe'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','warehousing.stock_transfers','transfer_id','INSERT')
    OR NOT has_column_privilege('salekhpos_runtime','warehousing.stock_transfers','status','UPDATE')
 THEN RAISE EXCEPTION 'Warehousing write grants are incomplete'; END IF;
END $$;
ROLLBACK;
