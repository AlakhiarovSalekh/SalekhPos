BEGIN;
CREATE SCHEMA subscriptions;
REVOKE ALL ON SCHEMA subscriptions FROM PUBLIC;
CREATE TABLE subscriptions.plans(
  plan_id uuid PRIMARY KEY,
  code text NOT NULL UNIQUE CHECK(char_length(code) BETWEEN 1 AND 40),
  name text NOT NULL CHECK(char_length(name) BETWEEN 1 AND 120),
  price numeric(18,2) NOT NULL CHECK(price>=0),
  currency char(3) NOT NULL CHECK(currency~'^[A-Z]{3}$'),
  billing_interval text NOT NULL CHECK(billing_interval IN('monthly','annual')),
  entitlements jsonb NOT NULL CHECK(jsonb_typeof(entitlements)='object'),
  is_active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp()
);
CREATE TABLE subscriptions.subscriptions(
  organization_id uuid NOT NULL REFERENCES organization.organizations,
  subscription_id uuid NOT NULL,
  plan_id uuid NOT NULL REFERENCES subscriptions.plans,
  operation_id uuid NOT NULL,
  request_hash char(64) NOT NULL CHECK(request_hash~'^[0-9a-f]{64}$'),
  status text NOT NULL CHECK(status IN('trialing','active','past_due','canceled','expired')),
  period_start timestamptz NOT NULL,
  period_end timestamptz NOT NULL CHECK(period_end>period_start),
  cancel_at_period_end boolean NOT NULL DEFAULT false,
  canceled_at timestamptz NULL,
  issuer text NOT NULL,
  subject text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  row_version bigint NOT NULL DEFAULT 1 CHECK(row_version>0),
  PRIMARY KEY(organization_id,subscription_id),
  UNIQUE(organization_id,operation_id)
);
CREATE UNIQUE INDEX ux_subscriptions_current ON subscriptions.subscriptions(organization_id)
  WHERE status IN('trialing','active','past_due');
CREATE TABLE subscriptions.operations(
  organization_id uuid NOT NULL,
  subscription_id uuid NOT NULL,
  operation_id uuid NOT NULL,
  request_hash char(64) NOT NULL CHECK(request_hash~'^[0-9a-f]{64}$'),
  operation_type text NOT NULL CHECK(operation_type IN('change_plan','cancel','renew','mark_past_due','expire')),
  occurred_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  issuer text NOT NULL,
  subject text NOT NULL,
  PRIMARY KEY(organization_id,operation_id),
  FOREIGN KEY(organization_id,subscription_id) REFERENCES subscriptions.subscriptions(organization_id,subscription_id)
);
CREATE TABLE subscriptions.entitlement_snapshots(
  organization_id uuid NOT NULL,
  subscription_id uuid NOT NULL,
  row_version bigint NOT NULL,
  limits jsonb NOT NULL CHECK(jsonb_typeof(limits)='object'),
  valid_until timestamptz NOT NULL,
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  PRIMARY KEY(organization_id,subscription_id,row_version),
  FOREIGN KEY(organization_id,subscription_id) REFERENCES subscriptions.subscriptions(organization_id,subscription_id)
);
CREATE INDEX ix_subscription_operations ON subscriptions.operations(organization_id,subscription_id,occurred_at,operation_id);
ALTER TABLE subscriptions.subscriptions ENABLE ROW LEVEL SECURITY; ALTER TABLE subscriptions.subscriptions FORCE ROW LEVEL SECURITY;
ALTER TABLE subscriptions.operations ENABLE ROW LEVEL SECURITY; ALTER TABLE subscriptions.operations FORCE ROW LEVEL SECURITY;
ALTER TABLE subscriptions.entitlement_snapshots ENABLE ROW LEVEL SECURITY; ALTER TABLE subscriptions.entitlement_snapshots FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON subscriptions.subscriptions USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON subscriptions.operations USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
CREATE POLICY tenant_isolation ON subscriptions.entitlement_snapshots USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid) WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA subscriptions TO salekhpos_runtime;
GRANT SELECT ON subscriptions.plans TO salekhpos_runtime;
GRANT SELECT,INSERT ON subscriptions.subscriptions,subscriptions.operations,subscriptions.entitlement_snapshots TO salekhpos_runtime;
GRANT UPDATE(plan_id,status,period_start,period_end,cancel_at_period_end,canceled_at,row_version) ON subscriptions.subscriptions TO salekhpos_runtime;
COMMIT;
