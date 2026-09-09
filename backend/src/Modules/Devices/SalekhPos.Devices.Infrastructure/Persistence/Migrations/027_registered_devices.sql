BEGIN;
DO $$ BEGIN IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF; END $$;
CREATE SCHEMA devices; REVOKE ALL ON SCHEMA devices FROM PUBLIC;
CREATE TABLE devices.registered_devices(
 organization_id uuid NOT NULL, device_id uuid NOT NULL, operation_id uuid NOT NULL, branch_id uuid NOT NULL,
 register_id uuid NOT NULL, code varchar(64) NOT NULL, name varchar(100) NOT NULL, platform varchar(16) NOT NULL,
 status varchar(16) NOT NULL, sync_protocol_version integer NOT NULL, registered_at timestamptz NOT NULL,
 issuer text NOT NULL, registered_by text NOT NULL,
 PRIMARY KEY(organization_id,device_id), UNIQUE(organization_id,operation_id), UNIQUE(organization_id,branch_id,code),
 FOREIGN KEY(organization_id,branch_id,register_id) REFERENCES stores.registers(organization_id,branch_id,register_id),
 CHECK(code~'^[A-Za-z0-9_-]{1,64}$'), CHECK(char_length(name) BETWEEN 1 AND 100),
 CHECK(platform IN('desktop','mobile','kiosk')), CHECK(status='active'), CHECK(sync_protocol_version=1));
CREATE INDEX ix_registered_devices_branch_cursor ON devices.registered_devices(organization_id,branch_id,device_id);
ALTER TABLE devices.registered_devices ENABLE ROW LEVEL SECURITY; ALTER TABLE devices.registered_devices FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON devices.registered_devices USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA devices TO salekhpos_runtime; GRANT SELECT ON devices.registered_devices TO salekhpos_runtime;
GRANT INSERT(organization_id,device_id,operation_id,branch_id,register_id,code,name,platform,status,sync_protocol_version,registered_at,issuer,registered_by) ON devices.registered_devices TO salekhpos_runtime;
COMMIT;
