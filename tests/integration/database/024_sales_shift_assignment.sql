DO $$ BEGIN
 IF NOT EXISTS(SELECT FROM pg_constraint WHERE conname='fk_completed_sales_shift') THEN RAISE EXCEPTION 'sale shift reference missing'; END IF;
 IF NOT EXISTS(SELECT FROM pg_indexes WHERE schemaname='sales' AND indexname='ix_completed_sales_shift') THEN RAISE EXCEPTION 'sale shift access path missing'; END IF;
END $$;
