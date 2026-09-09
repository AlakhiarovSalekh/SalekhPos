DO $$ BEGIN
IF NOT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='sales' AND table_name='suspended_carts' AND column_name='completed_sale_id') THEN RAISE EXCEPTION 'completed sale link missing'; END IF;
IF has_column_privilege('salekhpos_runtime','sales.suspended_carts','completed_sale_id','INSERT') THEN RAISE EXCEPTION 'runtime can forge cart sale link on insert'; END IF;
END $$;
