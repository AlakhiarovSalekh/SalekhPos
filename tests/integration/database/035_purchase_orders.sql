BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('purchase_orders'),('purchase_order_lines')) v(t)
   LEFT JOIN pg_class c ON c.oid=('purchasing.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Purchasing tables must force RLS'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','purchasing.purchase_orders','SELECT')
    OR NOT has_table_privilege('salekhpos_runtime','purchasing.purchase_order_lines','SELECT')
    OR has_table_privilege('salekhpos_runtime','purchasing.purchase_orders','DELETE')
 THEN RAISE EXCEPTION 'Purchasing runtime privileges are unsafe'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','purchasing.purchase_orders','order_id','INSERT')
    OR NOT has_column_privilege('salekhpos_runtime','purchasing.purchase_orders','status','UPDATE')
    OR NOT has_column_privilege('salekhpos_runtime','purchasing.purchase_order_lines','line_total','INSERT')
 THEN RAISE EXCEPTION 'Purchasing write grants are incomplete'; END IF;
END $$;
ROLLBACK;
