\set ON_ERROR_STOP on
-- Disposable test database only; run after the before-fixture and migration 002.
BEGIN;

DO $$ BEGIN
    IF NOT EXISTS (SELECT FROM organization.branches
        WHERE organization_id = '90000000-0000-0000-0000-000000000001'
          AND branch_id = '90000000-0000-0000-0000-000000000002'
          AND code = 'LEGACY' AND name = 'Legacy' || chr(9) || 'branch'
          AND created_at = '2020-01-02T03:04:05Z'::timestamptz
          AND business_id IS NULL AND region_id IS NULL AND time_zone_id IS NULL
          AND NOT is_configured) THEN
        RAISE EXCEPTION 'Migration changed or configured legacy branch data';
    END IF;
    IF EXISTS (SELECT FROM organization.businesses
               WHERE organization_id = '90000000-0000-0000-0000-000000000001') THEN
        RAISE EXCEPTION 'Migration invented a legacy business';
    END IF;
    IF EXISTS (SELECT FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'organization' AND c.relname IN ('businesses', 'regions')
          AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity)) THEN
        RAISE EXCEPTION 'New hierarchy tables lack enforced RLS';
    END IF;
END $$;

INSERT INTO organization.organizations (organization_id, name) VALUES
('11111111-1111-1111-1111-111111111111', 'Organization A'),
('22222222-2222-2222-2222-222222222222', 'Organization B');
INSERT INTO organization.businesses (organization_id, business_id, code, name) VALUES
('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000001', 'RETAIL', 'Retail A'),
('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000002', 'OTHER', 'Other A'),
('22222222-2222-2222-2222-222222222222', 'bbbbbbbb-0000-0000-0000-000000000001', 'RETAIL', 'Retail B');
INSERT INTO organization.regions (organization_id, business_id, region_id, code, name) VALUES
('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000001001', 'WEST', 'West A'),
('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000002', 'aaaaaaaa-0000-0000-0000-000000001002', 'WEST', 'Other West A'),
('22222222-2222-2222-2222-222222222222', 'bbbbbbbb-0000-0000-0000-000000000001', 'bbbbbbbb-0000-0000-0000-000000001001', 'WEST', 'West B');

-- Exercise every table constraint, not just the shared validation function.
DO $$
DECLARE candidate text; whitespace integer; codepoint integer; invalid_names text[] := ARRAY['', repeat('a', 201)];
BEGIN
    FOREACH codepoint IN ARRAY ARRAY[1, 9, 10, 31, 127, 133, 159] LOOP
        invalid_names := array_append(invalid_names, 'Store' || chr(codepoint) || 'Name');
    END LOOP;
    FOREACH whitespace IN ARRAY ARRAY[32, 160, 5760, 8192, 8202, 8232, 8233, 8239, 8287, 12288] LOOP
        invalid_names := invalid_names || ARRAY[chr(whitespace), chr(whitespace) || 'Store', 'Store' || chr(whitespace)];
    END LOOP;
    FOREACH candidate IN ARRAY invalid_names LOOP
        BEGIN
            INSERT INTO organization.organizations (organization_id, name) VALUES (gen_random_uuid(), candidate);
            RAISE EXCEPTION 'Invalid organization name accepted: %', encode(convert_to(candidate, 'UTF8'), 'hex');
        EXCEPTION WHEN check_violation THEN NULL; END;
        BEGIN
            INSERT INTO organization.businesses (organization_id, business_id, code, name)
            VALUES ('11111111-1111-1111-1111-111111111111', gen_random_uuid(), 'INVALID', candidate);
            RAISE EXCEPTION 'Invalid business name accepted';
        EXCEPTION WHEN check_violation THEN NULL; END;
        BEGIN
            INSERT INTO organization.regions (organization_id, business_id, region_id, code, name)
            VALUES ('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000001', gen_random_uuid(), 'INVALID', candidate);
            RAISE EXCEPTION 'Invalid region name accepted';
        EXCEPTION WHEN check_violation THEN NULL; END;
        BEGIN
            INSERT INTO organization.branches (organization_id, business_id, branch_id, code, name, time_zone_id)
            VALUES ('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000001', gen_random_uuid(), 'INVALID', candidate, 'Etc/UTC');
            RAISE EXCEPTION 'Invalid branch name accepted';
        EXCEPTION WHEN check_violation THEN NULL; END;
    END LOOP;
    FOREACH candidate IN ARRAY ARRAY['თბილისის მაღაზია', 'Şəki mağazası', 'متجر', 'Store' || chr(160) || 'Name', repeat(U&'\+01F3EA', 200)] LOOP
        INSERT INTO organization.organizations (organization_id, name) VALUES (gen_random_uuid(), candidate);
    END LOOP;
    BEGIN
        INSERT INTO organization.regions (organization_id, business_id, region_id, code, name)
        VALUES ('11111111-1111-1111-1111-111111111111', 'bbbbbbbb-0000-0000-0000-000000000001', gen_random_uuid(), 'CROSS', 'Illegal');
        RAISE EXCEPTION 'Region referenced another tenant business';
    EXCEPTION WHEN foreign_key_violation THEN NULL; END;
END $$;

SET LOCAL ROLE salekhpos_runtime;
DO $$ BEGIN
    IF EXISTS (SELECT FROM organization.businesses) OR EXISTS (SELECT FROM organization.regions) THEN
        RAISE EXCEPTION 'Missing tenant context exposed hierarchy';
    END IF;
    BEGIN
        INSERT INTO organization.branches (organization_id, business_id, branch_id, code, name, time_zone_id)
        VALUES ('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000001', gen_random_uuid(), 'MISSING', 'Illegal', 'Etc/UTC');
        RAISE EXCEPTION 'Missing tenant context allowed a write';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;

SELECT set_config('app.organization_id', '11111111-1111-1111-1111-111111111111', true);
DO $$
DECLARE candidate text;
BEGIN
    IF (SELECT count(*) FROM organization.businesses) <> 2 OR (SELECT count(*) FROM organization.regions) <> 2 THEN
        RAISE EXCEPTION 'Tenant A hierarchy isolation failed';
    END IF;
    INSERT INTO organization.branches (organization_id, business_id, region_id, branch_id, code, name, time_zone_id)
    VALUES ('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000001001',
            'cccccccc-0000-0000-0000-000000000001', 'WITH_REGION', 'Valid region branch', 'Asia/Tbilisi');
    INSERT INTO organization.branches (organization_id, business_id, branch_id, code, name, time_zone_id)
    VALUES ('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000001',
            'cccccccc-0000-0000-0000-000000000002', 'NO_REGION', 'Valid branch', 'America/New_York');
    IF (SELECT count(*) FROM organization.branches WHERE is_configured) <> 2 THEN
        RAISE EXCEPTION 'Valid branches did not become configured';
    END IF;
    BEGIN
        INSERT INTO organization.branches (organization_id, branch_id, code, name)
        VALUES ('11111111-1111-1111-1111-111111111111', gen_random_uuid(), 'UNCONFIGURED', 'Illegal');
        RAISE EXCEPTION 'New branch was accepted without business or time zone';
    EXCEPTION WHEN check_violation THEN NULL; END;
    BEGIN
        INSERT INTO organization.branches (organization_id, business_id, branch_id, code, name, time_zone_id)
        VALUES ('11111111-1111-1111-1111-111111111111', 'bbbbbbbb-0000-0000-0000-000000000001', gen_random_uuid(), 'CROSS_BUSINESS', 'Illegal', 'Etc/UTC');
        RAISE EXCEPTION 'Branch referenced another tenant business';
    EXCEPTION WHEN foreign_key_violation THEN NULL; END;
    BEGIN
        INSERT INTO organization.branches (organization_id, business_id, region_id, branch_id, code, name, time_zone_id)
        VALUES ('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000001002', gen_random_uuid(), 'CROSS_REGION', 'Illegal', 'Etc/UTC');
        RAISE EXCEPTION 'Branch referenced a region under another business';
    EXCEPTION WHEN foreign_key_violation THEN NULL; END;
    FOREACH candidate IN ARRAY ARRAY['', 'Asia/Tbilisi ', 'asia/tbilisi', 'Eastern Standard Time', 'UTC+4', '+04:00', 'Mars/Phobos'] LOOP
        BEGIN
            INSERT INTO organization.branches (organization_id, business_id, branch_id, code, name, time_zone_id)
            VALUES ('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-0000-0000-0000-000000000001', gen_random_uuid(), 'INVALID_ZONE', 'Illegal', candidate);
            RAISE EXCEPTION 'Invalid IANA time zone accepted: %', candidate;
        EXCEPTION WHEN check_violation THEN NULL; END;
    END LOOP;
    BEGIN
        UPDATE organization.branches SET business_id = 'aaaaaaaa-0000-0000-0000-000000000002';
        RAISE EXCEPTION 'Runtime could reparent a branch';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        INSERT INTO organization.businesses (organization_id, business_id, code, name)
        VALUES ('11111111-1111-1111-1111-111111111111', gen_random_uuid(), 'NO_GRANT', 'Illegal');
        RAISE EXCEPTION 'Runtime could provision a business';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        UPDATE organization.regions SET name = 'Illegal';
        RAISE EXCEPTION 'Runtime could modify a region';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        DELETE FROM organization.businesses;
        RAISE EXCEPTION 'Runtime could delete a business';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        ALTER TABLE organization.regions DISABLE ROW LEVEL SECURITY;
        RAISE EXCEPTION 'Runtime could disable region RLS';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;

SELECT set_config('app.organization_id', '22222222-2222-2222-2222-222222222222', true);
DO $$ BEGIN
    IF (SELECT count(*) FROM organization.businesses) <> 1 OR (SELECT count(*) FROM organization.regions) <> 1
       OR EXISTS (SELECT FROM organization.branches) THEN
        RAISE EXCEPTION 'Tenant B hierarchy isolation failed';
    END IF;
END $$;

SELECT set_config('app.organization_id', 'invalid-uuid', true);
DO $$ BEGIN
    BEGIN
        PERFORM * FROM organization.businesses;
        RAISE EXCEPTION 'Malformed tenant context was accepted';
    EXCEPTION WHEN invalid_text_representation THEN NULL; END;
END $$;

RESET ROLE;
DO $$ BEGIN
    BEGIN
        UPDATE organization.branches SET business_id = NULL, time_zone_id = NULL, region_id = NULL
        WHERE branch_id = 'cccccccc-0000-0000-0000-000000000002';
        RAISE EXCEPTION 'Configured branch became unconfigured';
    EXCEPTION WHEN check_violation THEN NULL; END;
    UPDATE organization.branches SET name = 'Legacy branch'
    WHERE branch_id = '90000000-0000-0000-0000-000000000002';
    IF NOT EXISTS (SELECT FROM organization.branches WHERE branch_id = '90000000-0000-0000-0000-000000000002' AND NOT is_configured) THEN
        RAISE EXCEPTION 'Legacy name repair unexpectedly configured branch';
    END IF;
END $$;
ROLLBACK;
\echo Business hierarchy, Unicode invariants and legacy migration checks passed.
