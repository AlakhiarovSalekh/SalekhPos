BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('inbox'),('preferences')) v(t)
   LEFT JOIN pg_class c ON c.oid=('notifications.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Notification tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='notifications'
   AND indexname='ix_notifications_recipient_cursor')
 THEN RAISE EXCEPTION 'Notification recipient cursor index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','notifications.inbox','SELECT')
   OR NOT has_column_privilege('salekhpos_runtime','notifications.inbox','is_read','UPDATE')
   OR has_column_privilege('salekhpos_runtime','notifications.inbox','body','UPDATE')
 THEN RAISE EXCEPTION 'Notification grants are unsafe'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','notifications.preferences','push_enabled','UPDATE')
 THEN RAISE EXCEPTION 'Notification preference grants are incomplete'; END IF;
END $$;
ROLLBACK;
