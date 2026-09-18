BEGIN;

DO $$ BEGIN
  IF NOT EXISTS (
      SELECT
      FROM pg_class c
      JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname='integrations'
        AND c.relname='webhook_payloads'
        AND c.relrowsecurity
        AND c.relforcerowsecurity)
  THEN
    RAISE EXCEPTION 'Webhook payload table must force RLS';
  END IF;

  IF NOT has_table_privilege('salekhpos_runtime','integrations.webhook_payloads','SELECT')
    OR NOT has_table_privilege('salekhpos_runtime','integrations.webhook_payloads','INSERT')
    OR has_table_privilege('salekhpos_runtime','integrations.webhook_payloads','UPDATE')
    OR has_table_privilege('salekhpos_runtime','integrations.webhook_payloads','DELETE')
  THEN
    RAISE EXCEPTION 'Webhook payload runtime grants are unsafe';
  END IF;

  IF NOT EXISTS (
      SELECT
      FROM pg_constraint c
      JOIN pg_class t ON t.oid=c.conrelid
      JOIN pg_namespace n ON n.oid=t.relnamespace
      WHERE n.nspname='integrations'
        AND t.relname='webhook_payloads'
        AND c.contype='f'
        AND pg_get_constraintdef(c.oid) LIKE '%webhook_deliveries%')
  THEN
    RAISE EXCEPTION 'Webhook payload delivery foreign key is missing';
  END IF;

  IF NOT EXISTS (
      SELECT
      FROM pg_policies
      WHERE schemaname='integrations'
        AND tablename='webhook_payloads'
        AND policyname='tenant_isolation')
  THEN
    RAISE EXCEPTION 'Webhook payload tenant isolation policy is missing';
  END IF;
END $$;

-- Validate payload and digest constraints without leaving test data behind.
DO $$ BEGIN
  BEGIN
    INSERT INTO integrations.webhook_payloads(
      organization_id,delivery_id,payload_sha256,payload)
    VALUES(
      gen_random_uuid(),
      gen_random_uuid(),
      repeat('z',64),
      decode('01','hex'));
    RAISE EXCEPTION 'Invalid webhook payload digest accepted';
  EXCEPTION
    WHEN check_violation THEN NULL;
    WHEN foreign_key_violation THEN
      RAISE EXCEPTION 'Digest validation did not run before foreign-key validation';
  END;

  BEGIN
    INSERT INTO integrations.webhook_payloads(
      organization_id,delivery_id,payload_sha256,payload)
    VALUES(
      gen_random_uuid(),
      gen_random_uuid(),
      repeat('a',64),
      ''::bytea);
    RAISE EXCEPTION 'Empty webhook payload accepted';
  EXCEPTION
    WHEN check_violation THEN NULL;
    WHEN foreign_key_violation THEN
      RAISE EXCEPTION 'Payload-size validation did not run before foreign-key validation';
  END;
END $$;

ROLLBACK;
