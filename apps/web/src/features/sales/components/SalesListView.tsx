"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { UUID_PATTERN } from "@/lib/boundedJson";
import { useSalesPage } from "../hooks/useSalesPage";
import { ManagerShell } from "./ManagerShell";
import { ScopeSelector } from "./ScopeSelector";
import { EmptyPanel, ErrorPanel, LoadingPanel } from "./StatePanel";
import { SaleRow } from "./SaleRow";

export function SalesListView({ organizationId, branchId, after }: { organizationId?: string; branchId?: string; after?: string }) {
  const scoped = Boolean(organizationId && branchId && UUID_PATTERN.test(organizationId) && UUID_PATTERN.test(branchId));
  return <ManagerShell organizationId={scoped ? organizationId : undefined} branchId={scoped ? branchId : undefined}>
    <div className="manager-content">
      <div className="page-heading"><div><p className="eyebrow">SALES CONTROL</p><h1>Sales ledger</h1><p>Immutable sale totals, reversal status and captured payment evidence.</p></div><Link className="secondary-button" href="/returns">Return history</Link></div>
      <ScopeSelector view="sales" organizationId={organizationId} branchId={branchId} />
      {scoped && <SalesTable organizationId={organizationId!} branchId={branchId!} after={after} />}
    </div>
  </ManagerShell>;
}

function SalesTable({ organizationId, branchId, after }: { organizationId: string; branchId: string; after?: string }) {
  const router = useRouter();
  const state = useSalesPage(organizationId, branchId, after && UUID_PATTERN.test(after) ? after : undefined);
  if (state.status === "loading") return <LoadingPanel label="Loading sales and payment evidence…" />;
  if (state.status === "error") return <ErrorPanel error={state.error} retry={() => router.refresh()} />;
  if (state.page.items.length === 0) return <EmptyPanel title="No completed sales" detail="There are no completed sale records in this branch and page." />;
  const base = `/sales?organization=${encodeURIComponent(organizationId)}&branch=${encodeURIComponent(branchId)}`;
  return <section className="data-card" aria-labelledby="sales-table-title">
    <div className="card-heading"><div><h2 id="sales-table-title">Completed sale records</h2><p>{state.page.items.length} records on this page</p></div></div>
    <div className="table-scroll"><table><thead><tr><th>Sale</th><th>Status</th><th>Total</th><th>Payment</th><th>Register</th></tr></thead><tbody>
      {state.page.items.map(sale => <SaleRow key={sale.id} sale={sale} organizationId={organizationId} branchId={branchId} />)}
    </tbody></table></div>
    <div className="pagination"><button className="secondary-button" type="button" disabled={!after} onClick={() => router.back()}>Previous</button>{state.page.nextCursor ? <Link className="secondary-button" href={`${base}&after=${encodeURIComponent(state.page.nextCursor)}`}>Next page</Link> : <span className="muted">End of ledger</span>}</div>
  </section>;
}
