BEGIN;
SET LOCAL ROLE salekhpos_systemadministration;

CREATE FUNCTION system_administration.list_super_admins(
    p_issuer text,
    p_subject text,
    p_after uuid,
    p_limit integer)
RETURNS SETOF jsonb
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path=pg_catalog,pg_temp
AS $$
BEGIN
    IF p_issuer IS NULL OR char_length(p_issuer) NOT BETWEEN 1 AND 2048
        OR p_subject IS NULL OR char_length(p_subject) NOT BETWEEN 1 AND 256
        OR p_after='00000000-0000-0000-0000-000000000000'
        OR p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 101 THEN
        RAISE EXCEPTION 'Invalid platform query' USING ERRCODE='22023';
    END IF;
    IF NOT EXISTS(
        SELECT FROM system_administration.super_admins
        WHERE issuer=p_issuer AND subject=p_subject AND revoked_at IS NULL) THEN
        RAISE EXCEPTION 'Platform authority required' USING ERRCODE='42501';
    END IF;
    RETURN QUERY
    SELECT system_administration.admin_response(a)
    FROM system_administration.super_admins a
    WHERE p_after IS NULL OR a.admin_id>p_after
    ORDER BY a.admin_id
    LIMIT p_limit;
END $$;

CREATE FUNCTION system_administration.list_authority_audit(
    p_issuer text,
    p_subject text,
    p_after uuid,
    p_limit integer)
RETURNS SETOF jsonb
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path=pg_catalog,pg_temp
AS $$
BEGIN
    IF p_issuer IS NULL OR char_length(p_issuer) NOT BETWEEN 1 AND 2048
        OR p_subject IS NULL OR char_length(p_subject) NOT BETWEEN 1 AND 256
        OR p_after='00000000-0000-0000-0000-000000000000'
        OR p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 101 THEN
        RAISE EXCEPTION 'Invalid platform query' USING ERRCODE='22023';
    END IF;
    IF NOT EXISTS(
        SELECT FROM system_administration.super_admins
        WHERE issuer=p_issuer AND subject=p_subject AND revoked_at IS NULL) THEN
        RAISE EXCEPTION 'Platform authority required' USING ERRCODE='42501';
    END IF;
    RETURN QUERY
    SELECT jsonb_build_object(
        'operationId',a.operation_id,
        'action',a.action,
        'actorSubject',a.actor_subject,
        'targetKey',a.target_key,
        'targetId',a.target_id,
        'reason',a.reason,
        'traceId',lower(a.trace_id),
        'recordedAt',a.recorded_at)
    FROM system_administration.authority_audit a
    WHERE p_after IS NULL OR a.operation_id>p_after
    ORDER BY a.operation_id
    LIMIT p_limit;
END $$;

REVOKE ALL ON FUNCTION system_administration.list_super_admins(text,text,uuid,integer) FROM PUBLIC;
REVOKE ALL ON FUNCTION system_administration.list_authority_audit(text,text,uuid,integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION system_administration.list_super_admins(text,text,uuid,integer) TO salekhpos_runtime;
GRANT EXECUTE ON FUNCTION system_administration.list_authority_audit(text,text,uuid,integer) TO salekhpos_runtime;
RESET ROLE;
COMMIT;
