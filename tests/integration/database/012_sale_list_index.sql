BEGIN;
DO $$ BEGIN
 IF NOT EXISTS(SELECT FROM pg_indexes WHERE schemaname='sales'
   AND tablename='completed_sales' AND indexname='completed_sales_branch_page')
 THEN RAISE EXCEPTION 'sale list index missing'; END IF;
END $$;
ROLLBACK;
