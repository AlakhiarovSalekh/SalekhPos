BEGIN;

SET LOCAL ROLE salekhpos_bootstrap;
SELECT system_administration.bootstrap_root(
  '52000000-0000-0000-0000-000000000001',
  'https://platform-read.test',
  'root-owner',
  'Regression test root',
  repeat('a',32));
RESET ROLE;

SET LOCAL ROLE salekhpos_runtime;
DO $$ BEGIN
  IF NOT has_function_privilege(
      'salekhpos_runtime',
      'system_administration.list_super_admins(text,text,uuid,integer)',
      'EXECUTE')
    OR NOT has_function_privilege(
      'salekhpos_runtime',
      'system_administration.list_authority_audit(text,text,uuid,integer)',
      'EXECUTE')
  THEN
    RAISE EXCEPTION 'Platform authority read functions are not executable by runtime';
  END IF;

  BEGIN
    PERFORM * FROM system_administration.super_admins;
    RAISE EXCEPTION 'Runtime direct super-admin enumeration allowed';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;

  BEGIN
    PERFORM * FROM system_administration.authority_audit;
    RAISE EXCEPTION 'Runtime direct authority-audit enumeration allowed';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;

  IF (SELECT count(*) FROM system_administration.list_super_admins(
      'https://platform-read.test','root-owner',NULL,100)) <> 1
  THEN
    RAISE EXCEPTION 'Root could not enumerate platform authorities';
  END IF;

  IF (SELECT count(*) FROM system_administration.list_authority_audit(
      'https://platform-read.test','root-owner',NULL,100)) <> 1
  THEN
    RAISE EXCEPTION 'Root could not enumerate authority audit';
  END IF;

  BEGIN
    PERFORM * FROM system_administration.list_super_admins(
      'https://platform-read.test','ordinary-user',NULL,100);
    RAISE EXCEPTION 'Non-admin platform authority enumeration allowed';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;

  BEGIN
    PERFORM * FROM system_administration.list_authority_audit(
      'https://platform-read.test','ordinary-user',NULL,100);
    RAISE EXCEPTION 'Non-admin authority-audit enumeration allowed';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;

  BEGIN
    PERFORM * FROM system_administration.list_super_admins(
      'https://platform-read.test','root-owner',
      '00000000-0000-0000-0000-000000000000',100);
    RAISE EXCEPTION 'Zero cursor accepted';
  EXCEPTION WHEN invalid_parameter_value THEN NULL; END;

  BEGIN
    PERFORM * FROM system_administration.list_authority_audit(
      'https://platform-read.test','root-owner',NULL,102);
    RAISE EXCEPTION 'Oversized platform audit page accepted';
  EXCEPTION WHEN invalid_parameter_value THEN NULL; END;
END $$;
RESET ROLE;

ROLLBACK;
