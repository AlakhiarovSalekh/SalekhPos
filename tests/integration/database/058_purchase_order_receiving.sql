BEGIN;

DO $$ BEGIN
  IF NOT EXISTS (
      SELECT FROM pg_constraint c
      JOIN pg_class t ON t.oid=c.conrelid
      JOIN pg_namespace n ON n.oid=t.relnamespace
      WHERE n.nspname='purchasing' AND t.relname='purchase_orders'
        AND c.conname='purchase_orders_status_check'
        AND pg_get_constraintdef(c.oid) LIKE '%partially_received%'
        AND pg_get_constraintdef(c.oid) LIKE '%received%')
  THEN RAISE EXCEPTION 'Purchase order receiving statuses are not enforced'; END IF;

  IF NOT EXISTS (
      SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname='purchasing' AND c.relname='purchase_receipts'
        AND c.relrowsecurity AND c.relforcerowsecurity)
    OR NOT EXISTS (
      SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname='purchasing' AND c.relname='purchase_receipt_lines'
        AND c.relrowsecurity AND c.relforcerowsecurity)
  THEN RAISE EXCEPTION 'Purchase receipt tables must force RLS'; END IF;

  IF NOT has_table_privilege('salekhpos_runtime','purchasing.purchase_receipts','SELECT,INSERT')
    OR has_table_privilege('salekhpos_runtime','purchasing.purchase_receipts','UPDATE,DELETE,TRUNCATE')
    OR NOT has_table_privilege('salekhpos_runtime','purchasing.purchase_receipt_lines','SELECT,INSERT')
    OR has_table_privilege('salekhpos_runtime','purchasing.purchase_receipt_lines','UPDATE,DELETE,TRUNCATE')
  THEN RAISE EXCEPTION 'Purchase receipt runtime grants are unsafe'; END IF;

  IF NOT EXISTS (
      SELECT FROM pg_constraint c
      JOIN pg_class t ON t.oid=c.conrelid
      JOIN pg_namespace n ON n.oid=t.relnamespace
      WHERE n.nspname='purchasing' AND t.relname='purchase_receipt_lines'
        AND c.contype='f' AND pg_get_constraintdef(c.oid) LIKE '%inventory.stock_movements%')
  THEN RAISE EXCEPTION 'Purchase receipt lines must reference inventory movements'; END IF;

  IF NOT EXISTS (
      SELECT FROM pg_constraint c
      JOIN pg_class t ON t.oid=c.conrelid
      JOIN pg_namespace n ON n.oid=t.relnamespace
      WHERE n.nspname='purchasing' AND t.relname='purchase_receipt_lines'
        AND c.contype='f'
        AND pg_get_constraintdef(c.oid) LIKE '%purchase_receipts%'
        AND pg_get_constraintdef(c.oid) LIKE '%order_id%')
  THEN RAISE EXCEPTION 'Purchase receipt lines must be bound to their receipt order'; END IF;
END $$;

ROLLBACK;
