BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE INDEX ix_payment_records_branch_history ON payments.payment_records(organization_id,branch_id,completed_at DESC,payment_id DESC);
CREATE INDEX ix_refund_records_branch_history ON payments.refund_records(organization_id,branch_id,completed_at DESC,refund_id DESC);
CREATE INDEX ix_sale_voids_branch_history_time ON sales.sale_voids(organization_id,branch_id,voided_at DESC,void_id DESC);
COMMIT;
