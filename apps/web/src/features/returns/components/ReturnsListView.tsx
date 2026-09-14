"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { UUID_PATTERN } from "@/lib/boundedJson";
import { ManagerShell } from "@/features/sales/components/ManagerShell";
import { ScopeSelector } from "@/features/sales/components/ScopeSelector";
import { EmptyPanel, ErrorPanel, LoadingPanel } from "@/features/sales/components/StatePanel";
import { formatDate, formatMoney, shortId } from "@/features/sales/api";
import { useReturnsPage } from "../hooks/useReturnsPage";

export function ReturnsListView({ organizationId, branchId, after }: { organizationId?: string; branchId?: string; after?: string }) {
  const scoped = Boolean(organizationId && branchId && UUID_PATTERN.test(organizationId) && UUID_PATTERN.test(branchId));
  return <ManagerShell organizationId={scoped ? organizationId : undefined} branchId={scoped ? branchId : undefined}>
    <div className="manager-content">
      <div className="page-heading"><div><p className="eyebrow">REVERSAL CONTROL</p><h1>Return history</h1><p>Completed item returns and their atomic cash-refund evidence.</p></div><Link className="secondary-button" href="/sales">Find a sale</Link></div>
      <ScopeSelector view="returns" organizationId={organizationId} branchId={branchId} />
      {scoped && <ReturnsTable organizationId={organizationId!} branchId={branchId!} after={after} />}
    </div>
  </ManagerShell>;
}

function ReturnsTable({ organizationId, branchId, after }: { organizationId: string; branchId: string; after?: string }) {
  const router = useRouter();
  const state = useReturnsPage(organizationId, branchId, after && UUID_PATTERN.test(after) ? after : undefined);
  if (state.status === "loading") return <LoadingPanel label="Loading return history…" />;
  if (state.status === "error") return <ErrorPanel error={state.error} retry={() => router.refresh()} />;
  if (state.page.items.length === 0) return <EmptyPanel title="No completed returns" detail="No return records are visible in this branch and page." />;
  const query = `organization=${encodeURIComponent(organizationId)}&branch=${encodeURIComponent(branchId)}`;
  const base = `/returns?${query}`;
  return <section className="data-card"><div className="card-heading"><div><h2>Completed returns</h2><p>{state.page.items.length} records on this page</p></div></div><div className="table-scroll"><table><thead><tr><th>Return</th><th>Original sale</th><th>Refund amount</th><th>Reason</th><th>Completed</th></tr></thead><tbody>{state.page.items.map(item => <tr key={item.id}><td><Link className="record-link" href={`/returns/${item.id}?${query}`}>#{shortId(item.id)}</Link></td><td><Link className="record-link subtle" href={`/sales/${item.saleId}?${query}`}>#{shortId(item.saleId)}</Link></td><td><strong>{formatMoney(item.amount, item.currency)}</strong></td><td className="reason-cell">{item.reason}</td><td>{formatDate(item.completedAt)}</td></tr>)}</tbody></table></div><div className="pagination"><button className="secondary-button" type="button" disabled={!after} onClick={() => router.back()}>Previous</button>{state.page.nextCursor ? <Link className="secondary-button" href={`${base}&after=${encodeURIComponent(state.page.nextCursor)}`}>Next page</Link> : <span className="muted">End of history</span>}</div></section>;
}
