\set ON_ERROR_STOP on
-- Disposable test database only, after 001 tests and before migration 002.
-- This row deliberately exercises a formerly valid C0 character in a legacy name.
BEGIN;
INSERT INTO organization.organizations (organization_id, name, created_at)
VALUES ('90000000-0000-0000-0000-000000000001', 'Legacy organization', '2020-01-02T03:04:05Z');
INSERT INTO organization.branches (organization_id, branch_id, code, name, created_at)
VALUES ('90000000-0000-0000-0000-000000000001', '90000000-0000-0000-0000-000000000002',
        'LEGACY', 'Legacy' || chr(9) || 'branch', '2020-01-02T03:04:05Z');
COMMIT;
