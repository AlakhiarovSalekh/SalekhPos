BEGIN;

DO $$ BEGIN
  IF NOT EXISTS (
      SELECT
      FROM pg_class c
      JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname='notifications'
        AND c.relname='external_deliveries'
        AND c.relrowsecurity
        AND c.relforcerowsecurity)
  THEN
    RAISE EXCEPTION 'Notification external delivery queue must force RLS';
  END IF;

  IF NOT EXISTS (
      SELECT FROM pg_indexes
      WHERE schemaname='notifications'
        AND indexname='ix_notification_external_due')
  THEN
    RAISE EXCEPTION 'Notification external delivery due index is missing';
  END IF;

  IF NOT has_table_privilege('salekhpos_runtime','notifications.external_deliveries','SELECT')
    OR NOT has_table_privilege('salekhpos_runtime','notifications.external_deliveries','INSERT')
    OR has_table_privilege('salekhpos_runtime','notifications.external_deliveries','DELETE')
    OR has_table_privilege('salekhpos_runtime','notifications.external_deliveries','TRUNCATE')
    OR NOT has_column_privilege('salekhpos_runtime','notifications.external_deliveries','status','UPDATE')
    OR has_column_privilege('salekhpos_runtime','notifications.external_deliveries','recipient_subject','UPDATE')
  THEN
    RAISE EXCEPTION 'Notification external delivery runtime grants are unsafe';
  END IF;

  IF NOT EXISTS (
      SELECT FROM pg_policies
      WHERE schemaname='notifications'
        AND tablename='external_deliveries'
        AND policyname='tenant_isolation')
  THEN
    RAISE EXCEPTION 'Notification external delivery tenant isolation policy is missing';
  END IF;

  IF NOT EXISTS (
      SELECT
      FROM pg_constraint c
      JOIN pg_class t ON t.oid=c.conrelid
      JOIN pg_namespace n ON n.oid=t.relnamespace
      WHERE n.nspname='notifications'
        AND t.relname='external_deliveries'
        AND c.contype='u'
        AND pg_get_constraintdef(c.oid) LIKE '%notification_id%'
        AND pg_get_constraintdef(c.oid) LIKE '%channel%')
  THEN
    RAISE EXCEPTION 'Notification channel idempotency constraint is missing';
  END IF;
END $$;

ROLLBACK;
