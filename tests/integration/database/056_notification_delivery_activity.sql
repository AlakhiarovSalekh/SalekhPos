BEGIN;

DO $$ BEGIN
  IF NOT EXISTS (
      SELECT
      FROM pg_indexes
      WHERE schemaname='notifications'
        AND tablename='external_deliveries'
        AND indexname='ix_notification_external_activity'
        AND indexdef LIKE '%organization_id%'
        AND indexdef LIKE '%created_at DESC%'
        AND indexdef LIKE '%delivery_id DESC%')
  THEN
    RAISE EXCEPTION 'Notification delivery activity cursor index is missing or malformed';
  END IF;

  IF NOT has_table_privilege(
      'salekhpos_runtime',
      'notifications.external_deliveries',
      'SELECT')
    OR has_table_privilege(
      'salekhpos_runtime',
      'notifications.external_deliveries',
      'DELETE')
    OR has_table_privilege(
      'salekhpos_runtime',
      'notifications.external_deliveries',
      'TRUNCATE')
  THEN
    RAISE EXCEPTION 'Notification delivery activity migration changed runtime grants unsafely';
  END IF;

  IF NOT EXISTS (
      SELECT
      FROM pg_class c
      JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname='notifications'
        AND c.relname='external_deliveries'
        AND c.relrowsecurity
        AND c.relforcerowsecurity)
  THEN
    RAISE EXCEPTION 'Notification delivery activity table must continue to force RLS';
  END IF;
END $$;

ROLLBACK;
