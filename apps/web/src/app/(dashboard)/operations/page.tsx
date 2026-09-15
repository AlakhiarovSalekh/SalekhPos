import { ManagerShell } from "@/features/sales/components/ManagerShell";
import { ReconciliationWorkspace } from "@/features/operations/ReconciliationWorkspace";

export default function OperationsPage() {
  return <ManagerShell><div className="manager-content"><div className="page-heading"><div><p className="eyebrow">OPERATIONS</p><h1>Reconciliation<span className="accent">.</span></h1><p>Monitor shifts, cash history and payment events across the selected branch.</p></div></div><ReconciliationWorkspace /></div></ManagerShell>;
}
