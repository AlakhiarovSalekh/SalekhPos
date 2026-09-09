DO $$ BEGIN
IF (SELECT count(*) FROM pg_indexes WHERE indexname IN('ix_payment_records_branch_history','ix_refund_records_branch_history','ix_sale_voids_branch_history_time'))<>3
THEN RAISE EXCEPTION 'payment event history indexes missing'; END IF;
END $$;
