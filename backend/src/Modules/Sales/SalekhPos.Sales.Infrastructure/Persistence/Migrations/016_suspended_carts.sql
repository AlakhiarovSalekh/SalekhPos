BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE TABLE sales.suspended_carts(
 organization_id uuid NOT NULL, cart_id uuid NOT NULL, operation_id uuid NOT NULL, branch_id uuid NOT NULL,
 note text, suspended_at timestamptz NOT NULL, expires_at timestamptz NOT NULL, resumed_at timestamptz,
 issuer text NOT NULL, subject text NOT NULL,
 PRIMARY KEY(organization_id,cart_id), UNIQUE(organization_id,operation_id),
 CHECK(note IS NULL OR char_length(note) BETWEEN 1 AND 500), CHECK(expires_at>suspended_at),
 FOREIGN KEY(organization_id,branch_id) REFERENCES organization.branches(organization_id,branch_id));
CREATE TABLE sales.suspended_cart_lines(
 organization_id uuid NOT NULL, cart_id uuid NOT NULL, line_number integer NOT NULL CHECK(line_number BETWEEN 1 AND 500),
 product_id uuid NOT NULL, quantity numeric(20,6) NOT NULL CHECK(quantity>0),
 PRIMARY KEY(organization_id,cart_id,line_number), UNIQUE(organization_id,cart_id,product_id),
 FOREIGN KEY(organization_id,cart_id) REFERENCES sales.suspended_carts(organization_id,cart_id),
 FOREIGN KEY(organization_id,product_id) REFERENCES catalog.products(organization_id,product_id));
ALTER TABLE sales.suspended_carts ENABLE ROW LEVEL SECURITY; ALTER TABLE sales.suspended_carts FORCE ROW LEVEL SECURITY;
ALTER TABLE sales.suspended_cart_lines ENABLE ROW LEVEL SECURITY; ALTER TABLE sales.suspended_cart_lines FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON sales.suspended_carts USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON sales.suspended_cart_lines USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE INDEX suspended_carts_owner_active ON sales.suspended_carts(organization_id,branch_id,issuer,subject,expires_at) WHERE resumed_at IS NULL;
CREATE FUNCTION sales.enforce_cart_resume() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog AS $$
BEGIN
 IF OLD.resumed_at IS NOT NULL OR OLD.expires_at<=statement_timestamp()
    OR OLD.issuer<>current_setting('app.issuer',true) OR OLD.subject<>current_setting('app.subject',true)
    OR NEW IS DISTINCT FROM OLD AND (NEW.organization_id,NEW.cart_id,NEW.operation_id,NEW.branch_id,NEW.note,
       NEW.suspended_at,NEW.expires_at,NEW.issuer,NEW.subject) IS DISTINCT FROM
      (OLD.organization_id,OLD.cart_id,OLD.operation_id,OLD.branch_id,OLD.note,
       OLD.suspended_at,OLD.expires_at,OLD.issuer,OLD.subject)
 THEN RAISE EXCEPTION 'Suspended cart transition rejected'; END IF;
 NEW.resumed_at=statement_timestamp(); RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION sales.enforce_cart_resume() FROM PUBLIC;
CREATE TRIGGER enforce_cart_resume BEFORE UPDATE ON sales.suspended_carts FOR EACH ROW EXECUTE FUNCTION sales.enforce_cart_resume();
GRANT SELECT ON sales.suspended_carts,sales.suspended_cart_lines TO salekhpos_runtime;
GRANT INSERT(organization_id,cart_id,operation_id,branch_id,note,suspended_at,expires_at,issuer,subject) ON sales.suspended_carts TO salekhpos_runtime;
GRANT UPDATE(resumed_at) ON sales.suspended_carts TO salekhpos_runtime;
GRANT INSERT(organization_id,cart_id,line_number,product_id,quantity) ON sales.suspended_cart_lines TO salekhpos_runtime;
COMMIT;
