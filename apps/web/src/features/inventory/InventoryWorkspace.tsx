"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { getAccess, getSession, getStock, SafeApiError } from "./api";
import { MovementForm } from "./MovementForm";
import { StockTable } from "./StockTable";
import type { InventoryAccess, Session, StockItem } from "./types";
import { validateOrganizationId } from "./validation";

export function InventoryWorkspace() {
  const [session, setSession] = useState<Session>(); const [sessionError, setSessionError] = useState(false);
  const [organizationId, setOrganizationId] = useState(""); const [organizationError, setOrganizationError] = useState<string>();
  const [access, setAccess] = useState<InventoryAccess>(); const [branchId, setBranchId] = useState("");
  const [items, setItems] = useState<StockItem[]>([]); const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loadingContext, setLoadingContext] = useState(false); const [loadingStock, setLoadingStock] = useState(false); const [loadingMore, setLoadingMore] = useState(false); const [error, setError] = useState<string>();
  useEffect(() => { let active = true; getSession().then(value => { if (active) { setSession(value); setOrganizationId(sessionStorage.getItem("salekhpos.inventory.organization") ?? ""); } }).catch(() => { if (active) setSessionError(true); }); return () => { active = false; }; }, []);
  const loadPage = async (org: string, branch: string, after?: string) => {
    const page = await getStock(org, branch, after); setItems(current => after ? [...current, ...page.items] : page.items); setNextCursor(page.nextCursor);
  };
  const loadContext = async (event: React.FormEvent) => {
    event.preventDefault(); const org = organizationId.trim(); const invalid = validateOrganizationId(org); setOrganizationError(invalid ?? undefined); if (invalid) return;
    setLoadingContext(true); setError(undefined); setAccess(undefined); setItems([]); setNextCursor(null);
    try { const result = await getAccess(org); setAccess(result); sessionStorage.setItem("salekhpos.inventory.organization", org); const first = result.branches.find(branch => branch.canView); setBranchId(first?.branchId ?? ""); if (first) { setLoadingStock(true); await loadPage(org, first.branchId); } }
    catch (e) { setError(e instanceof SafeApiError ? e.message : "Inventory context could not be loaded."); }
    finally { setLoadingContext(false); setLoadingStock(false); }
  };
  const changeBranch = async (value: string) => { setBranchId(value); setItems([]); setNextCursor(null); setError(undefined); if (!value) return; setLoadingStock(true); try { await loadPage(organizationId.trim(), value); } catch (e) { setError(e instanceof SafeApiError ? e.message : "Stock could not be loaded."); } finally { setLoadingStock(false); } };
  const refresh = async () => { setError(undefined); setLoadingStock(true); try { await loadPage(organizationId.trim(), branchId); } catch (e) { setError(e instanceof SafeApiError ? e.message : "Stock could not be refreshed."); } finally { setLoadingStock(false); } };
  const more = async () => { if (!nextCursor) return; setLoadingMore(true); setError(undefined); try { await loadPage(organizationId.trim(), branchId, nextCursor); } catch (e) { setError(e instanceof SafeApiError ? e.message : "The next page could not be loaded."); } finally { setLoadingMore(false); } };
  if (sessionError) return <State title="We couldn’t open inventory" text="Your session could not be loaded. Try again." retry />;
  if (!session) return <p role="status" className="quiet">Opening inventory…</p>;
  if (!session.configured) return <State title="Sign-in is not configured" text="Contact your administrator before opening inventory." />;
  if (!session.authenticated) return <State title="Sign in required" text="Inventory is available only inside an authenticated workspace." signIn />;
  const branch = access?.branches.find(value => value.branchId === branchId);
  return <>
    <header className="inventory-header"><div><p className="eyebrow">INVENTORY MANAGEMENT</p><h1>Stock, without guesswork<span className="accent">.</span></h1><p>Signed in as {session.name || "your account"}. Server permissions and tenant isolation are checked on every request.</p></div><Link className="text-link" href="/dashboard">Dashboard</Link></header>
    <form className="context-card" onSubmit={loadContext} noValidate><label>Organization ID<input value={organizationId} onChange={e => setOrganizationId(e.target.value)} placeholder="00000000-0000-0000-0000-000000000000" aria-invalid={Boolean(organizationError)} />{organizationError && <span className="field-error">{organizationError}</span>}</label><button className="button compact" disabled={loadingContext}>{loadingContext ? "Loading access…" : "Load inventory"}</button>{access && <label>Authorized branch<select value={branchId} onChange={e => void changeBranch(e.target.value)}><option value="">No viewable branch</option>{access.branches.filter(value => value.canView).map(value => <option key={value.branchId} value={value.branchId}>{value.code} — {value.name}</option>)}</select></label>}</form>
    {error && <div role="alert" className="error inventory-error">{error} <button type="button" onClick={() => branchId ? void refresh() : undefined}>Try again</button></div>}
    {access && !access.branches.some(value => value.canView) && <div className="empty-state"><h2>No inventory access</h2><p>No active branch in this organization grants your session inventory.view.</p></div>}
    {loadingStock && !items.length ? <div role="status" className="loading-card">Loading current stock…</div> : branch && <div className="inventory-grid"><StockTable items={items} hasMore={Boolean(nextCursor)} loadingMore={loadingMore} onLoadMore={() => void more()} />{session.csrfToken ? <MovementForm organizationId={organizationId.trim()} branch={branch} products={items} csrfToken={session.csrfToken} onCreated={refresh} /> : <div role="alert" className="state-card">A CSRF token is unavailable, so stock changes are disabled.</div>}</div>}
  </>;
}

function State({ title, text, retry, signIn }: { title: string; text: string; retry?: boolean; signIn?: boolean }) { return <div className="state-card"><h2>{title}</h2><p>{text}</p>{retry && <button className="button compact" onClick={() => window.location.reload()}>Try again</button>}{signIn && <Link className="button compact" href="/sign-in">Go to sign in</Link>}</div>; }
