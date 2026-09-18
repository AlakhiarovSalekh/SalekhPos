BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('accounts'),('invoices'),('invoice_lines'),('charges'),('credit_notes')) v(t)
   LEFT JOIN pg_class c ON c.oid=('billing.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Billing tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='billing'
   AND indexname='ix_billing_invoices_account')
 THEN RAISE EXCEPTION 'Billing account invoice index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','billing.invoices','INSERT')
   OR NOT has_table_privilege('salekhpos_runtime','billing.charges','SELECT')
 THEN RAISE EXCEPTION 'Billing runtime grants are incomplete'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','billing.invoices','paid_amount','UPDATE')
   OR has_column_privilege('salekhpos_runtime','billing.invoices','invoice_number','UPDATE')
   OR has_table_privilege('salekhpos_runtime','billing.invoices','DELETE')
 THEN RAISE EXCEPTION 'Billing invoice mutation grants are unsafe'; END IF;
END $$;
ROLLBACK;
