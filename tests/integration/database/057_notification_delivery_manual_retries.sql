BEGIN;

DO $$ BEGIN
  IF NOT EXISTS (
      SELECT
      FROM pg_class c
      JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname='notifications'
        AND c.relname='delivery_manual_retries'
        AND c.relrowsecurity
        AND c.relforcerowsecurity)
  THEN
    RAISE EXCEPTION 'Notification delivery manual retry audit must force RLS';
  END IF;

  IF NOT has_table_privilege(
      'salekhpos_runtime','notifications.delivery_manual_retries','SELECT')
    OR NOT has_table_privilege(
      'salekhpos_runtime','notifications.delivery_manual_retries','INSERT')
    OR has_table_privilege(
      'salekhpos_runtime','notifications.delivery_manual_retries','UPDATE')
    OR has_table_privilege(
      'salekhpos_runtime','notifications.delivery_manual_retries','DELETE')
    OR has_table_privilege(
      'salekhpos_runtime','notifications.delivery_manual_retries','TRUNCATE')
  THEN
    RAISE EXCEPTION 'Notification delivery manual retry audit grants are unsafe';
  END IF;

  IF NOT EXISTS (
      SELECT FROM pg_policies
      WHERE schemaname='notifications'
        AND tablename='delivery_manual_retries'
        AND policyname='tenant_isolation')
  THEN
    RAISE EXCEPTION 'Notification delivery manual retry tenant isolation policy is missing';
  END IF;

  IF NOT EXISTS (
      SELECT
      FROM pg_constraint c
      JOIN pg_class t ON t.oid=c.conrelid
      JOIN pg_namespace n ON n.oid=t.relnamespace
      WHERE n.nspname='notifications'
        AND t.relname='delivery_manual_retries'
        AND c.contype='u'
        AND pg_get_constraintdef(c.oid) LIKE '%operation_id%')
  THEN
    RAISE EXCEPTION 'Notification delivery manual retry idempotency constraint is missing';
  END IF;

  IF NOT EXISTS (
      SELECT
      FROM pg_constraint c
      JOIN pg_class t ON t.oid=c.conrelid
      JOIN pg_namespace n ON n.oid=t.relnamespace
      WHERE n.nspname='notifications'
        AND t.relname='delivery_manual_retries'
        AND c.contype='f'
        AND pg_get_constraintdef(c.oid) LIKE '%external_deliveries%')
  THEN
    RAISE EXCEPTION 'Notification delivery manual retry foreign key is missing';
  END IF;
END $$;

ROLLBACK;
