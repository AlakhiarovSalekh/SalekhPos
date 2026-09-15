BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT FROM (VALUES ('employees'),('store_assignments')) v(t)
   LEFT JOIN pg_class c ON c.oid=('employees.'||v.t)::regclass
   WHERE NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
 THEN RAISE EXCEPTION 'Employee tables must force RLS'; END IF;
 IF NOT EXISTS (SELECT FROM pg_indexes WHERE schemaname='employees' AND indexname='ix_employee_assignments_branch')
 THEN RAISE EXCEPTION 'Employee branch assignment index is missing'; END IF;
 IF NOT has_table_privilege('salekhpos_runtime','employees.employees','SELECT')
    OR NOT has_table_privilege('salekhpos_runtime','employees.store_assignments','SELECT')
    OR has_table_privilege('salekhpos_runtime','employees.employees','DELETE')
 THEN RAISE EXCEPTION 'Employee runtime privileges are unsafe'; END IF;
 IF NOT has_column_privilege('salekhpos_runtime','employees.employees','employee_id','INSERT')
    OR NOT has_column_privilege('salekhpos_runtime','employees.employees','job_title','UPDATE')
 THEN RAISE EXCEPTION 'Employee write grants are incomplete'; END IF;
END $$;
ROLLBACK;
