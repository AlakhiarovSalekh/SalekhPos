BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
ALTER TABLE returns.completed_returns DROP CONSTRAINT completed_returns_organization_id_sale_id_key;
ALTER TABLE returns.return_lines ADD CONSTRAINT return_lines_product_once UNIQUE(organization_id,return_id,product_id);
CREATE INDEX completed_returns_sale_lookup ON returns.completed_returns(organization_id,sale_id);
CREATE INDEX return_lines_product_lookup ON returns.return_lines(organization_id,product_id,return_id);
COMMIT;
