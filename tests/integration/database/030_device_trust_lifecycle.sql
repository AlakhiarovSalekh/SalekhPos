BEGIN;
DO $$
BEGIN
 IF NOT EXISTS (
   SELECT FROM pg_constraint
   WHERE conrelid='devices.registered_devices'::regclass
     AND conname='registered_devices_status_check'
     AND pg_get_constraintdef(oid) LIKE '%pending%active%revoked%')
 THEN RAISE EXCEPTION 'Device lifecycle status constraint is missing'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_class
   WHERE oid='devices.device_credentials'::regclass
     AND relrowsecurity AND relforcerowsecurity)
 THEN RAISE EXCEPTION 'Device credentials must enforce RLS'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_constraint
   WHERE conrelid='devices.device_credentials'::regclass
     AND contype='f'
     AND confrelid='devices.registered_devices'::regclass)
 THEN RAISE EXCEPTION 'Device credentials must be tenant-linked to registered devices'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_attribute
   WHERE attrelid='devices.device_credentials'::regclass
     AND attname='public_key_fingerprint' AND NOT attisdropped AND attnotnull
     AND atttypid='bytea'::regtype)
 THEN RAISE EXCEPTION 'Credential fingerprint must be required bytea'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_constraint
   WHERE conrelid='devices.device_credentials'::regclass AND contype='c'
     AND pg_get_constraintdef(oid) LIKE '%octet_length(public_key_fingerprint) = 32%')
 THEN RAISE EXCEPTION 'Credential fingerprint must be exactly 32 bytes'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_class i JOIN pg_index x ON x.indexrelid=i.oid
   WHERE i.relnamespace='devices'::regnamespace
     AND i.relname='ux_device_credentials_live_fingerprint'
     AND x.indrelid='devices.device_credentials'::regclass AND x.indisunique
     AND pg_get_indexdef(i.oid) LIKE '%(organization_id, public_key_fingerprint)%'
     AND pg_get_expr(x.indpred,x.indrelid) LIKE '%pending%'
     AND pg_get_expr(x.indpred,x.indrelid) LIKE '%active%')
 THEN RAISE EXCEPTION 'Live credential fingerprints must be tenant-unique'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','devices.registered_devices','status','UPDATE')
    OR NOT has_column_privilege('salekhpos_runtime','devices.device_credentials','status','UPDATE')
 THEN RAISE EXCEPTION 'Runtime role lacks lifecycle update privileges'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','devices.device_credentials','public_key_fingerprint','INSERT')
    OR has_column_privilege('salekhpos_runtime','devices.device_credentials','public_key_fingerprint','UPDATE')
    OR has_column_privilege('salekhpos_runtime','devices.device_credentials','public_key_spki','UPDATE')
    OR has_column_privilege('salekhpos_runtime','devices.device_credentials','proof_challenge','UPDATE')
    OR has_table_privilege('salekhpos_runtime','devices.device_credentials','DELETE')
    OR has_table_privilege('salekhpos_runtime','devices.device_credentials','TRUNCATE')
 THEN RAISE EXCEPTION 'Runtime credential privileges are not least-privilege'; END IF;
END $$;
ROLLBACK;
