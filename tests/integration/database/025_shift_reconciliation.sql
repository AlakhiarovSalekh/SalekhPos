DO $$ BEGIN
 IF has_table_privilege('salekhpos_runtime','shifts.shifts','DELETE') THEN RAISE EXCEPTION 'runtime shift deletion must remain denied'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','shifts.shifts','expected_cash','UPDATE') THEN RAISE EXCEPTION 'runtime cannot close shifts'; END IF;
 IF NOT EXISTS(SELECT FROM information_schema.columns WHERE table_schema='returns' AND table_name='completed_returns' AND column_name='shift_id') THEN RAISE EXCEPTION 'return shift evidence missing'; END IF;
 IF NOT EXISTS(SELECT FROM information_schema.columns WHERE table_schema='payments' AND table_name='void_refunds' AND column_name='shift_id') THEN RAISE EXCEPTION 'void refund shift evidence missing'; END IF;
END $$;
