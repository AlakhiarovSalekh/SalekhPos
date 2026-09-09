BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
ALTER TABLE sales.suspended_carts ADD COLUMN completed_sale_id uuid;
ALTER TABLE sales.suspended_carts ADD CONSTRAINT suspended_cart_completed_sale UNIQUE(organization_id,completed_sale_id);
ALTER TABLE sales.suspended_carts ADD CONSTRAINT suspended_cart_completed_sale_fk FOREIGN KEY(organization_id,completed_sale_id) REFERENCES sales.completed_sales(organization_id,sale_id);
GRANT UPDATE(resumed_at,completed_sale_id) ON sales.suspended_carts TO salekhpos_runtime;
COMMIT;
