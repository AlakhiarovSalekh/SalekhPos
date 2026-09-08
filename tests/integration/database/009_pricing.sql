BEGIN;
INSERT INTO organization.organizations(organization_id,name) VALUES('11111111-1111-1111-1111-111111111111','Pricing tenant');
INSERT INTO organization.businesses(organization_id,business_id,code,name) VALUES('11111111-1111-1111-1111-111111111111','91000000-0000-0000-0000-000000000001','MAIN','Main');
INSERT INTO organization.branches(organization_id,business_id,branch_id,code,name,time_zone_id) VALUES('11111111-1111-1111-1111-111111111111','91000000-0000-0000-0000-000000000001','92000000-0000-0000-0000-000000000001','MAIN','Main','Asia/Tbilisi');
SELECT set_config('app.issuer','sql-test',true),set_config('app.subject','pricing-test',true);
INSERT INTO catalog.products(organization_id,product_id,operation_id,sku,name,unit_code) VALUES('11111111-1111-1111-1111-111111111111','93000000-0000-0000-0000-000000000001','94000000-0000-0000-0000-000000000001','PRICE','Price test','EA');
SET ROLE salekhpos_runtime;
SELECT set_config('app.organization_id','11111111-1111-1111-1111-111111111111',true);
INSERT INTO pricing.prices(organization_id,price_id,operation_id,product_id,amount,currency,tax_mode,tax_rate,valid_from,issuer,subject)
VALUES('11111111-1111-1111-1111-111111111111','95000000-0000-0000-0000-000000000001','96000000-0000-0000-0000-000000000001','93000000-0000-0000-0000-000000000001',10.25,'GEL','inclusive',18,'2026-01-01Z','test','test');
DO $$ BEGIN
 IF (SELECT amount FROM pricing.prices)<>10.25 THEN RAISE EXCEPTION 'price unavailable'; END IF;
 BEGIN UPDATE pricing.prices SET amount=20; RAISE EXCEPTION 'price update accepted'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN DELETE FROM pricing.prices; RAISE EXCEPTION 'price delete accepted'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN
  INSERT INTO pricing.prices(organization_id,price_id,operation_id,product_id,amount,currency,tax_mode,tax_rate,valid_from,issuer,subject)
  VALUES('11111111-1111-1111-1111-111111111111','95000000-0000-0000-0000-000000000002','96000000-0000-0000-0000-000000000002','93000000-0000-0000-0000-000000000001',12,'GEL','inclusive',18,'2026-06-01Z','test','test');
  RAISE EXCEPTION 'overlapping price accepted';
 EXCEPTION WHEN exclusion_violation THEN NULL; END;
END $$;
SELECT set_config('app.organization_id','22222222-2222-2222-2222-222222222222',true);
DO $$ BEGIN IF EXISTS(SELECT FROM pricing.prices) THEN RAISE EXCEPTION 'cross tenant price exposed'; END IF; END $$;
ROLLBACK;
