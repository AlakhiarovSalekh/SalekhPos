BEGIN;
DO $$ BEGIN
  IF current_user='salekhpos_runtime' THEN RAISE EXCEPTION 'Runtime role must not run migrations'; END IF;
  IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='salekhpos_runtime') THEN RAISE EXCEPTION 'Provision runtime role'; END IF;
END $$;

CREATE SCHEMA loyalty;
REVOKE ALL ON SCHEMA loyalty FROM PUBLIC;
CREATE TABLE loyalty.accounts(
  organization_id uuid NOT NULL,
  account_id uuid NOT NULL CHECK(account_id<>'00000000-0000-0000-0000-000000000000'),
  operation_id uuid NOT NULL CHECK(operation_id<>'00000000-0000-0000-0000-000000000000'),
  customer_id uuid NOT NULL,
  tier varchar(16) NOT NULL CHECK(tier IN('bronze','silver','gold','platinum')),
  points_balance bigint NOT NULL DEFAULT 0 CHECK(points_balance>=0),
  lifetime_points bigint NOT NULL DEFAULT 0 CHECK(lifetime_points>=0),
  row_version bigint NOT NULL DEFAULT 1 CHECK(row_version>0),
  created_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  issuer text NOT NULL CHECK(char_length(issuer) BETWEEN 1 AND 2048),
  subject text NOT NULL CHECK(char_length(subject) BETWEEN 1 AND 256),
  PRIMARY KEY(organization_id,account_id),
  UNIQUE(organization_id,operation_id),
  UNIQUE(organization_id,customer_id),
  FOREIGN KEY(organization_id,customer_id) REFERENCES customers.customers(organization_id,customer_id),
  CHECK(points_balance<=lifetime_points)
);
CREATE INDEX loyalty_accounts_cursor ON loyalty.accounts(organization_id,account_id);

CREATE TABLE loyalty.point_events(
  organization_id uuid NOT NULL,
  event_id uuid NOT NULL CHECK(event_id<>'00000000-0000-0000-0000-000000000000'),
  operation_id uuid NOT NULL CHECK(operation_id<>'00000000-0000-0000-0000-000000000000'),
  account_id uuid NOT NULL,
  kind varchar(16) NOT NULL CHECK(kind IN('earn','redeem')),
  points_delta integer NOT NULL CHECK(points_delta<>0 AND abs(points_delta)<=1000000),
  balance_after bigint NOT NULL CHECK(balance_after>=0),
  lifetime_points_after bigint NOT NULL CHECK(lifetime_points_after>=0),
  tier_after varchar(16) NOT NULL CHECK(tier_after IN('bronze','silver','gold','platinum')),
  version_after bigint NOT NULL CHECK(version_after>0),
  reason varchar(200) NOT NULL CHECK(reason=btrim(reason) AND char_length(reason) BETWEEN 1 AND 200),
  occurred_at timestamptz NOT NULL DEFAULT statement_timestamp(),
  issuer text NOT NULL CHECK(char_length(issuer) BETWEEN 1 AND 2048),
  subject text NOT NULL CHECK(char_length(subject) BETWEEN 1 AND 256),
  PRIMARY KEY(organization_id,event_id),
  UNIQUE(organization_id,operation_id),
  FOREIGN KEY(organization_id,account_id) REFERENCES loyalty.accounts(organization_id,account_id)
);
CREATE INDEX loyalty_events_cursor ON loyalty.point_events(organization_id,account_id,event_id);
ALTER TABLE loyalty.accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE loyalty.accounts FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON loyalty.accounts
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
ALTER TABLE loyalty.point_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE loyalty.point_events FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON loyalty.point_events
 USING(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid)
 WITH CHECK(organization_id=nullif(current_setting('app.organization_id',true),'')::uuid);
GRANT USAGE ON SCHEMA loyalty TO salekhpos_runtime;
GRANT SELECT ON loyalty.accounts,loyalty.point_events TO salekhpos_runtime;
GRANT INSERT(organization_id,account_id,operation_id,customer_id,tier,issuer,subject)
  ON loyalty.accounts TO salekhpos_runtime;
GRANT UPDATE(tier,points_balance,lifetime_points,row_version,updated_at)
  ON loyalty.accounts TO salekhpos_runtime;
GRANT INSERT(organization_id,event_id,operation_id,account_id,kind,points_delta,balance_after,lifetime_points_after,tier_after,version_after,reason,issuer,subject)
  ON loyalty.point_events TO salekhpos_runtime;
COMMIT;
