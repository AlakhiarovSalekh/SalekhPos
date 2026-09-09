BEGIN;
DO $$ BEGIN
 IF NOT EXISTS(SELECT FROM pg_policies WHERE schemaname='devices' AND tablename='registered_devices' AND policyname='tenant_isolation') THEN RAISE EXCEPTION 'Device tenant policy missing'; END IF;
 IF has_table_privilege('salekhpos_runtime','devices.registered_devices','UPDATE') OR has_table_privilege('salekhpos_runtime','devices.registered_devices','DELETE') THEN RAISE EXCEPTION 'Runtime may mutate registered devices'; END IF;
END $$;
ROLLBACK;
