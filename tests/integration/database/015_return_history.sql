DO $$ BEGIN
IF NOT EXISTS(SELECT 1 FROM pg_indexes WHERE schemaname='returns' AND indexname='completed_returns_branch_page')
THEN RAISE EXCEPTION 'return history index missing'; END IF;
END $$;
