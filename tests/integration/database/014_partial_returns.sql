DO $$ BEGIN
IF EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='returns.completed_returns'::regclass AND contype='u' AND pg_get_constraintdef(oid)='UNIQUE (organization_id, sale_id)')
THEN RAISE EXCEPTION 'sale-wide return uniqueness remains'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_indexes WHERE schemaname='returns' AND indexname='completed_returns_sale_lookup')
THEN RAISE EXCEPTION 'partial-return lookup index missing'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='returns.return_lines'::regclass AND conname='return_lines_product_once')
THEN RAISE EXCEPTION 'one product per return is not enforced'; END IF;
END $$;
