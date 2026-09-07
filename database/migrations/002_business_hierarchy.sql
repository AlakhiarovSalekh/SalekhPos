-- Additive hierarchy upgrade. Apply as the migration owner after migration 001.
-- Legacy branches are retained without inventing a business or local time zone.
BEGIN;

CREATE FUNCTION organization.is_valid_name(candidate text)
RETURNS boolean LANGUAGE sql IMMUTABLE PARALLEL SAFE
SET search_path = pg_catalog
RETURN candidate IS NOT NULL
    AND char_length(candidate) BETWEEN 1 AND 200
    AND candidate = btrim(candidate, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')
    AND candidate COLLATE "C" !~ U&'[\0001-\001F\007F-\009F]';

CREATE FUNCTION organization.is_valid_time_zone(candidate text)
RETURNS boolean LANGUAGE sql STABLE PARALLEL SAFE
SET search_path = pg_catalog
RETURN candidate IS NOT NULL
    AND char_length(candidate) BETWEEN 1 AND 255
    AND candidate COLLATE "C" ~ '^[A-Za-z0-9_+-]+(/[A-Za-z0-9_+-]+)*$'
    AND candidate NOT LIKE 'posix/%' AND candidate NOT LIKE 'right/%'
    AND EXISTS (SELECT FROM pg_catalog.pg_timezone_names WHERE name = candidate COLLATE "C");

REVOKE ALL ON FUNCTION organization.is_valid_name(text), organization.is_valid_time_zone(text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION organization.is_valid_name(text), organization.is_valid_time_zone(text) TO salekhpos_runtime;

-- New and modified rows obey the domain invariant. Existing invalid values remain
-- intact until explicitly repaired; validate these constraints after that review.
ALTER TABLE organization.organizations ADD CONSTRAINT organizations_name_unicode_v2
    CHECK (organization.is_valid_name(name)) NOT VALID;
ALTER TABLE organization.branches ADD CONSTRAINT branches_name_unicode_v2
    CHECK (organization.is_valid_name(name)) NOT VALID;

CREATE TABLE organization.businesses (
    organization_id uuid NOT NULL REFERENCES organization.organizations(organization_id),
    business_id uuid NOT NULL CHECK (business_id <> '00000000-0000-0000-0000-000000000000'),
    code text NOT NULL CHECK (code COLLATE "C" ~ '^[A-Z0-9][A-Z0-9_-]{0,31}$'),
    name text NOT NULL CHECK (organization.is_valid_name(name)),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, business_id),
    UNIQUE (organization_id, code)
);

CREATE TABLE organization.regions (
    organization_id uuid NOT NULL,
    business_id uuid NOT NULL,
    region_id uuid NOT NULL CHECK (region_id <> '00000000-0000-0000-0000-000000000000'),
    code text NOT NULL CHECK (code COLLATE "C" ~ '^[A-Z0-9][A-Z0-9_-]{0,31}$'),
    name text NOT NULL CHECK (organization.is_valid_name(name)),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, business_id, region_id),
    UNIQUE (organization_id, business_id, code),
    FOREIGN KEY (organization_id, business_id)
        REFERENCES organization.businesses(organization_id, business_id)
);

ALTER TABLE organization.branches
    ADD COLUMN business_id uuid,
    ADD COLUMN region_id uuid,
    ADD COLUMN time_zone_id text,
    ADD COLUMN is_configured boolean GENERATED ALWAYS AS
        (business_id IS NOT NULL AND time_zone_id IS NOT NULL) STORED,
    ADD CONSTRAINT branches_business_and_time_zone_together
        CHECK ((business_id IS NULL) = (time_zone_id IS NULL)),
    ADD CONSTRAINT branches_region_requires_business CHECK (region_id IS NULL OR business_id IS NOT NULL),
    ADD CONSTRAINT branches_time_zone_valid
        CHECK (time_zone_id IS NULL OR organization.is_valid_time_zone(time_zone_id)),
    ADD CONSTRAINT branches_business_fk FOREIGN KEY (organization_id, business_id)
        REFERENCES organization.businesses(organization_id, business_id),
    ADD CONSTRAINT branches_region_fk FOREIGN KEY (organization_id, business_id, region_id)
        REFERENCES organization.regions(organization_id, business_id, region_id),
    ADD CONSTRAINT branches_business_identity UNIQUE (organization_id, business_id, branch_id);

CREATE INDEX branches_business_region_idx ON organization.branches (organization_id, business_id, region_id);

CREATE FUNCTION organization.enforce_branch_configuration()
RETURNS trigger LANGUAGE plpgsql
SET search_path = pg_catalog
AS $$
BEGIN
    IF (TG_OP = 'INSERT' OR OLD.is_configured)
       AND (NEW.business_id IS NULL OR NEW.time_zone_id IS NULL) THEN
        RAISE EXCEPTION 'New and configured branches require an explicit business and IANA time zone'
            USING ERRCODE = '23514', CONSTRAINT = 'branches_configuration_required';
    END IF;
    RETURN NEW;
END $$;

REVOKE ALL ON FUNCTION organization.enforce_branch_configuration() FROM PUBLIC;
CREATE TRIGGER branches_configuration_required BEFORE INSERT OR UPDATE ON organization.branches
    FOR EACH ROW EXECUTE FUNCTION organization.enforce_branch_configuration();

ALTER TABLE organization.businesses ENABLE ROW LEVEL SECURITY;
ALTER TABLE organization.businesses FORCE ROW LEVEL SECURITY;
ALTER TABLE organization.regions ENABLE ROW LEVEL SECURITY;
ALTER TABLE organization.regions FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON organization.businesses
    USING (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid)
    WITH CHECK (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid);
CREATE POLICY tenant_isolation ON organization.regions
    USING (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid)
    WITH CHECK (organization_id = nullif(current_setting('app.organization_id', true), '')::uuid);

REVOKE ALL ON organization.businesses, organization.regions FROM PUBLIC;
GRANT SELECT ON organization.businesses, organization.regions TO salekhpos_runtime;
GRANT INSERT (business_id, region_id, time_zone_id) ON organization.branches TO salekhpos_runtime;

COMMIT;
