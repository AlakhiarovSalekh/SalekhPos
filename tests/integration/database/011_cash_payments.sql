BEGIN;
INSERT INTO organization.organizations(organization_id,name) VALUES('11111111-1111-1111-1111-111111111111','Payment tenant');
INSERT INTO organization.businesses(organization_id,business_id,code,name) VALUES('11111111-1111-1111-1111-111111111111','b1000000-0000-0000-0000-000000000001','MAIN','Main');
INSERT INTO organization.branches(organization_id,business_id,branch_id,code,name,time_zone_id) VALUES('11111111-1111-1111-1111-111111111111','b1000000-0000-0000-0000-000000000001','b2000000-0000-0000-0000-000000000001','MAIN','Main','Asia/Tbilisi');
SET ROLE salekhpos_runtime;
SELECT set_config('app.organization_id','11111111-1111-1111-1111-111111111111',true);
INSERT INTO sales.completed_sales(organization_id,sale_id,operation_id,branch_id,currency,net_total,tax_total,grand_total,cash_received,change_due,completed_at,issuer,subject)
 VALUES('11111111-1111-1111-1111-111111111111','b3000000-0000-0000-0000-000000000001','b4000000-0000-0000-0000-000000000001','b2000000-0000-0000-0000-000000000001','GEL',10,1.8,11.8,12,0.2,'2026-06-01Z','test','test');
DO $$ BEGIN
 IF (SELECT count(*) FROM payments.payment_records WHERE sale_id='b3000000-0000-0000-0000-000000000001')<>1 THEN RAISE EXCEPTION 'cash payment not captured'; END IF;
 IF (SELECT amount FROM payments.payment_records)<>11.8 OR (SELECT change_amount FROM payments.payment_records)<>0.2 THEN RAISE EXCEPTION 'payment snapshot incorrect'; END IF;
 BEGIN UPDATE payments.payment_records SET amount=1; RAISE EXCEPTION 'payment update accepted'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN INSERT INTO payments.payment_records(organization_id,payment_id,sale_id,branch_id,method,status,currency,amount,tendered,change_amount,completed_at) VALUES('11111111-1111-1111-1111-111111111111',gen_random_uuid(),'b3000000-0000-0000-0000-000000000001','b2000000-0000-0000-0000-000000000001','cash','completed','GEL',11.8,12,0.2,now()); RAISE EXCEPTION 'runtime payment insert accepted'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
SELECT set_config('app.organization_id','22222222-2222-2222-2222-222222222222',true);
DO $$ BEGIN IF EXISTS(SELECT FROM payments.payment_records) THEN RAISE EXCEPTION 'cross tenant payment exposed'; END IF; END $$;
ROLLBACK;
