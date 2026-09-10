BEGIN;
DO $$ BEGIN
 IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
END $$;
CREATE TABLE sync.message_results(
 organization_id uuid NOT NULL,
 message_id uuid NOT NULL,
 sale_id uuid NOT NULL,
 status varchar(16) NOT NULL CHECK(status IN('applied','rejected')),
 result_code varchar(32) NOT NULL CHECK(result_code IN('applied','shift_conflict','price_conflict','insufficient_stock','sale_conflict')),
 decided_at timestamptz NOT NULL,
 PRIMARY KEY(organization_id,message_id),
 FOREIGN KEY(organization_id,message_id) REFERENCES sync.ingested_messages(organization_id,message_id)
);
ALTER TABLE sync.message_results ENABLE ROW LEVEL SECURITY; ALTER TABLE sync.message_results FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON sync.message_results
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT SELECT ON sync.message_results TO salekhpos_runtime;
GRANT INSERT(organization_id,message_id,sale_id,status,result_code,decided_at) ON sync.message_results TO salekhpos_runtime;
COMMIT;
