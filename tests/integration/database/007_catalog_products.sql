BEGIN;
INSERT INTO organization.organizations(organization_id,name)
VALUES('11111111-1111-1111-1111-111111111111','Catalog tenant A'),
      ('22222222-2222-2222-2222-222222222222','Catalog tenant B');
SET ROLE salekhpos_runtime;
SELECT set_config('app.organization_id','11111111-1111-1111-1111-111111111111',true),
       set_config('app.issuer','https://identity.example.test',true), set_config('app.subject','catalog-test',true);
INSERT INTO catalog.products(organization_id,product_id,operation_id,sku,name,unit_code,barcode)
VALUES('11111111-1111-1111-1111-111111111111','71000000-0000-0000-0000-000000000001',
       '72000000-0000-0000-0000-000000000001','COFFEE.001','Coffee','EA','12345678');
DO $$ BEGIN
  IF (SELECT count(*) FROM catalog.products) <> 1 THEN RAISE EXCEPTION 'own tenant product hidden'; END IF;
END $$;
SELECT set_config('app.organization_id','22222222-2222-2222-2222-222222222222',true);
DO $$ BEGIN
  IF EXISTS(SELECT FROM catalog.products) THEN RAISE EXCEPTION 'cross-tenant product exposed'; END IF;
  BEGIN
    INSERT INTO catalog.products(organization_id,product_id,operation_id,sku,name,unit_code)
    VALUES('11111111-1111-1111-1111-111111111111',gen_random_uuid(),gen_random_uuid(),'FORGED','Forged','EA');
    RAISE EXCEPTION 'cross-tenant product insert accepted';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;
  BEGIN
    DELETE FROM catalog.products;
    RAISE EXCEPTION 'runtime product deletion accepted';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;
  BEGIN
    PERFORM 1 FROM catalog.product_audit;
    RAISE EXCEPTION 'runtime product audit read accepted';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
RESET ROLE;
DO $$ BEGIN
  IF (SELECT count(*) FROM catalog.product_audit WHERE product_id='71000000-0000-0000-0000-000000000001') <> 1
    THEN RAISE EXCEPTION 'product audit missing'; END IF;
END $$;
ROLLBACK;
