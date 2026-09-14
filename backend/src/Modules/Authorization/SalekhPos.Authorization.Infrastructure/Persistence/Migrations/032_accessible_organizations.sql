-- Narrow cross-tenant discovery for the authenticated identity. The API runtime
-- cannot query organization data globally; it can only execute this bounded function.
BEGIN;

DO $$ BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'salekhpos_access_reader') THEN
        CREATE ROLE salekhpos_access_reader NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'salekhpos_access_reader'
        AND (rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN
        RAISE EXCEPTION 'Unsafe organization access reader role';
    END IF;
END $$;

GRANT USAGE ON SCHEMA access, organization TO salekhpos_access_reader;
GRANT SELECT (organization_id, issuer, subject, is_active, valid_from, valid_until)
    ON access.memberships TO salekhpos_access_reader;
GRANT SELECT (organization_id, name, is_active)
    ON organization.organizations TO salekhpos_access_reader;

CREATE POLICY organization_access_discovery ON organization.organizations
    FOR SELECT TO salekhpos_access_reader USING (is_active);
CREATE POLICY membership_access_discovery ON access.memberships
    FOR SELECT TO salekhpos_access_reader
    USING (true);

CREATE INDEX membership_identity_access
    ON access.memberships (issuer, subject, organization_id)
    WHERE is_active;

GRANT CREATE ON SCHEMA access TO salekhpos_access_reader;
SET LOCAL ROLE salekhpos_access_reader;
CREATE FUNCTION access.list_accessible_organizations(
    p_issuer text,
    p_subject text,
    p_after uuid,
    p_limit integer)
RETURNS TABLE (organization_id uuid, name text)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    IF p_issuer IS NULL OR char_length(p_issuer) NOT BETWEEN 1 AND 2048
        OR p_issuer <> btrim(p_issuer) OR p_issuer ~ '[[:cntrl:]]'
        OR p_subject IS NULL OR char_length(p_subject) NOT BETWEEN 1 AND 256
        OR p_subject <> btrim(p_subject) OR p_subject ~ '[[:cntrl:]]'
        OR p_after = '00000000-0000-0000-0000-000000000000'
        OR p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 101 THEN
        RAISE EXCEPTION 'Invalid organization access query' USING ERRCODE = '22023';
    END IF;

    RETURN QUERY
    SELECT o.organization_id, o.name
    FROM access.memberships AS m
    JOIN organization.organizations AS o ON o.organization_id = m.organization_id
    WHERE m.issuer = p_issuer AND m.subject = p_subject
      AND m.is_active AND m.valid_from <= statement_timestamp()
      AND (m.valid_until IS NULL OR m.valid_until > statement_timestamp())
      AND o.is_active
      AND (p_after IS NULL OR o.organization_id > p_after)
    ORDER BY o.organization_id
    LIMIT p_limit;
END $$;
RESET ROLE;
REVOKE CREATE ON SCHEMA access FROM salekhpos_access_reader;
REVOKE ALL ON FUNCTION access.list_accessible_organizations(text, text, uuid, integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION access.list_accessible_organizations(text, text, uuid, integer) TO salekhpos_runtime;

COMMIT;
