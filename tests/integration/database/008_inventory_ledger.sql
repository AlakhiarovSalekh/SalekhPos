BEGIN;
INSERT INTO organization.organizations(organization_id,name) VALUES('11111111-1111-1111-1111-111111111111','Inventory tenant');
INSERT INTO organization.businesses(organization_id,business_id,code,name) VALUES('11111111-1111-1111-1111-111111111111','81000000-0000-0000-0000-000000000001','MAIN','Main');
INSERT INTO organization.branches(organization_id,business_id,branch_id,code,name,time_zone_id) VALUES('11111111-1111-1111-1111-111111111111','81000000-0000-0000-0000-000000000001','82000000-0000-0000-0000-000000000001','MAIN','Main','Etc/UTC');
SELECT set_config('app.issuer','sql-test',true),set_config('app.subject','inventory-test',true);
INSERT INTO catalog.products(organization_id,product_id,operation_id,sku,name,unit_code) VALUES('11111111-1111-1111-1111-111111111111','83000000-0000-0000-0000-000000000001','84000000-0000-0000-0000-000000000001','TEST','Test','EA');
SET ROLE salekhpos_runtime;
SELECT set_config('app.organization_id','11111111-1111-1111-1111-111111111111',true);
INSERT INTO inventory.stock_movements(organization_id,movement_id,operation_id,branch_id,product_id,kind,direction,quantity,occurred_at,issuer,subject)
VALUES('11111111-1111-1111-1111-111111111111','85000000-0000-0000-0000-000000000001','86000000-0000-0000-0000-000000000001','82000000-0000-0000-0000-000000000001','83000000-0000-0000-0000-000000000001','receipt',1,10,now(),'test','test');
DO $$ BEGIN
 IF (SELECT sum(direction*quantity) FROM inventory.stock_movements)<>10 THEN RAISE EXCEPTION 'ledger total incorrect'; END IF;
 BEGIN UPDATE inventory.stock_movements SET quantity=20; RAISE EXCEPTION 'ledger update accepted'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN DELETE FROM inventory.stock_movements; RAISE EXCEPTION 'ledger delete accepted'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
SELECT set_config('app.organization_id','22222222-2222-2222-2222-222222222222',true);
DO $$ BEGIN IF EXISTS(SELECT FROM inventory.stock_movements) THEN RAISE EXCEPTION 'cross tenant inventory exposed'; END IF; END $$;
ROLLBACK;
