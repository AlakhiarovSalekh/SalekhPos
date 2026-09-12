BEGIN;
DO $$
BEGIN
 IF NOT EXISTS (
   SELECT FROM pg_class
   WHERE oid='devices.request_proof_nonces'::regclass AND relrowsecurity AND relforcerowsecurity)
 THEN RAISE EXCEPTION 'Device request nonce evidence must enforce RLS'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_policies
   WHERE schemaname='devices' AND tablename='request_proof_nonces' AND policyname='tenant_isolation')
 THEN RAISE EXCEPTION 'Device request nonce tenant policy is missing'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_index
   WHERE indrelid='devices.request_proof_nonces'::regclass AND indisunique
     AND pg_get_indexdef(indexrelid) LIKE '%(organization_id, device_id, credential_id, nonce_hash)%')
 THEN RAISE EXCEPTION 'Credential nonce uniqueness is missing'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_class i JOIN pg_index x ON x.indexrelid=i.oid
   WHERE i.relnamespace='devices'::regnamespace AND i.relname='ix_request_proof_nonces_expiry'
     AND x.indrelid='devices.request_proof_nonces'::regclass
     AND pg_get_indexdef(i.oid) LIKE '%(organization_id, expires_at)%')
 THEN RAISE EXCEPTION 'Tenant expiry retention index is missing'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_constraint
   WHERE conrelid='devices.request_proof_nonces'::regclass AND contype='c'
     AND pg_get_constraintdef(oid) LIKE '%expires_at = (accepted_at + ''00:11:00''::interval)%')
 THEN RAISE EXCEPTION 'Bounded nonce retention constraint is missing'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_trigger
   WHERE tgrelid='devices.request_proof_nonces'::regclass
     AND tgname='protect_live_request_proof_nonces' AND NOT tgisinternal)
 THEN RAISE EXCEPTION 'Live nonce deletion protection is missing'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','devices.request_proof_nonces','nonce_hash','INSERT')
    OR has_table_privilege('salekhpos_runtime','devices.request_proof_nonces','DELETE')
    OR has_table_privilege('salekhpos_runtime','devices.request_proof_nonces','UPDATE')
    OR has_table_privilege('salekhpos_runtime','devices.request_proof_nonces','TRUNCATE')
    OR has_table_privilege('salekhpos_runtime','devices.request_proof_nonces','SELECT')
 THEN RAISE EXCEPTION 'Runtime nonce table privileges are not least-privilege'; END IF;
 IF NOT EXISTS (
   SELECT FROM pg_proc
   WHERE oid='devices.purge_expired_request_proof_nonces()'::regprocedure
     AND prosecdef
     AND proconfig @> ARRAY['search_path=pg_catalog']::text[]
     AND has_function_privilege('salekhpos_runtime',oid,'EXECUTE'))
 THEN RAISE EXCEPTION 'Runtime nonce purge must use the hardened definer function'; END IF;
 IF EXISTS (
   SELECT FROM pg_proc
   WHERE oid='devices.purge_expired_request_proof_nonces()'::regprocedure
     AND coalesce(proacl::text,'')~'(^|[{,])=X/')
 THEN RAISE EXCEPTION 'Nonce purge must not be publicly executable'; END IF;
 IF EXISTS (
   SELECT FROM pg_proc
   WHERE oid='devices.reject_live_request_proof_nonce_deletion()'::regprocedure
     AND coalesce(proacl::text,'')~'(^|[{,])=X/')
 THEN RAISE EXCEPTION 'Nonce deletion guard must not be publicly executable'; END IF;
END $$;
ROLLBACK;
