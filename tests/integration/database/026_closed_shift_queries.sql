BEGIN;
DO $$
BEGIN
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='shifts' AND indexname='ix_shifts_closed_branch_cursor') THEN
  RAISE EXCEPTION 'Closed shift branch cursor index is missing';
 END IF;
END $$;
ROLLBACK;
