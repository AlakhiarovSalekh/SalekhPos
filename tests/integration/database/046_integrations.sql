BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('connections'),('connection_state_changes'),('webhook_deliveries'),('webhook_attempts')) v(t)
   LEFT JOIN pg_class c ON c.oid=('integrations.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Integration tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='integrations'
   AND indexname='ix_integrations_webhooks_due')
 THEN RAISE EXCEPTION 'Webhook due index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','integrations.connections','INSERT')
   OR NOT has_table_privilege('salekhpos_runtime','integrations.webhook_attempts','SELECT')
 THEN RAISE EXCEPTION 'Integration runtime grants are incomplete'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','integrations.connections','status','UPDATE')
   OR has_column_privilege('salekhpos_runtime','integrations.connections','secret_reference','UPDATE')
   OR has_table_privilege('salekhpos_runtime','integrations.connections','DELETE')
 THEN RAISE EXCEPTION 'Integration connection mutation grants are unsafe'; END IF;
END $$;
ROLLBACK;
