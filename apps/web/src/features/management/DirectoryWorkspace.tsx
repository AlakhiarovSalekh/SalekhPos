"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { createCustomer, createEmployee, createSupplier, getCustomers, getEmployees, getSuppliers } from "./api";

type Kind = "customers" | "suppliers" | "employees";
type Row = { id: string; code: string; name: string; detail: string; active: boolean };

async function loadRows(kind: Kind, organizationId: string, branchId: string): Promise<Row[]> {
  if (kind === "customers") {
    const page = await getCustomers(organizationId);
    return page.items.map(item => ({ id: item.id, code: item.code, name: item.displayName,
      detail: item.email ?? item.phone ?? "", active: item.isActive }));
  }
  if (kind === "suppliers") {
    const page = await getSuppliers(organizationId);
    return page.items.map(item => ({ id: item.id, code: item.code, name: item.name,
      detail: item.taxId ?? item.email ?? "", active: item.isActive }));
  }
  const page = await getEmployees(organizationId, branchId);
  return page.items.map(item => ({ id: item.id, code: item.code, name: item.displayName,
    detail: item.jobTitle, active: item.isActive }));
}
export function DirectoryWorkspace({ kind }: { kind: Kind }) {
  const scope = useOperationsScope();
  const [rows, setRows] = useState<Row[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [extra, setExtra] = useState("");
  const title = kind === "customers" ? "Customers" : kind === "suppliers" ? "Suppliers" : "Employees";
  const ready = Boolean(scope.organizationId && (kind !== "employees" || scope.branchId));

  useEffect(() => {
    if (!ready) return;
    let live = true;
    loadRows(kind, scope.organizationId, scope.branchId).then(items => {
      if (!live) return;
      setRows(items);
      setError(null);
    }).catch(() => { if (live) setError(`${title} could not be loaded.`); });
    return () => { live = false; };
  }, [kind, ready, scope.branchId, scope.organizationId, title]);

  const active = useMemo(() => rows.filter(item => item.active).length, [rows]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!ready || !code.trim() || !name.trim()) return;
    setSaving(true); setError(null);
    try {
      if (kind === "customers") {
        await createCustomer(scope.organizationId, { code: code.trim().toUpperCase(), displayName: name.trim(),
          email: email.trim() || undefined, phone: phone.trim() || undefined });
      } else if (kind === "suppliers") {
        await createSupplier(scope.organizationId, { code: code.trim().toUpperCase(), name: name.trim(),
          taxId: extra.trim() || undefined, email: email.trim() || undefined, phone: phone.trim() || undefined });
      } else {
        await createEmployee(scope.organizationId, scope.branchId, { code: code.trim().toUpperCase(),
          displayName: name.trim(), email: email.trim() || undefined, phone: phone.trim() || undefined,
          jobTitle: extra.trim() || "Staff" });
      }
      setCode(""); setName(""); setEmail(""); setPhone(""); setExtra("");
      setRows(await loadRows(kind, scope.organizationId, scope.branchId));
    } catch { setError(`${title} could not be created.`); }
    finally { setSaving(false); }
  }

  return <section className="manager-content">
    <div className="manager-title"><div><span className="eyebrow">MANAGEMENT</span><h1>{title}</h1>
      <p>Organization-scoped records with safe server validation and role-based access.</p></div>
      <div className="metric-card"><strong>{rows.length}</strong><span>Total · {active} active</span></div></div>
    <OperationsScopeSelector scope={scope} />
    {scope.error ? <p className="error-banner">{scope.error}</p> : null}
    {error ? <p className="error-banner">{error}</p> : null}
    <div className="split-grid">
      <form className="panel" onSubmit={submit}><h2>Add {title.slice(0, -1)}</h2>
        <label>Code<input value={code} maxLength={40} onChange={event => setCode(event.target.value)} /></label>
        <label>Name<input value={name} maxLength={180} onChange={event => setName(event.target.value)} /></label>
        <label>Email<input value={email} maxLength={254} onChange={event => setEmail(event.target.value)} /></label>
        <label>Phone<input value={phone} maxLength={40} onChange={event => setPhone(event.target.value)} /></label>
        {kind !== "customers" ? <label>{kind === "employees" ? "Job title" : "Tax ID"}
          <input value={extra} maxLength={kind === "employees" ? 100 : 64} onChange={event => setExtra(event.target.value)} />
        </label> : null}
        <button disabled={!ready || saving}>{saving ? "Working…" : "Create"}</button>
      </form>
      <div className="panel"><h2>{title}</h2>
        {rows.length === 0 ? <p className="muted">No records yet.</p> : <div className="data-list">{rows.map(row =>
          <article key={row.id}><strong>{row.name}</strong><span>{row.code} · {row.detail || "No additional details"}</span>
            <small>{row.active ? "Active" : "Inactive"} · {row.id.slice(0, 8).toUpperCase()}</small></article>)}</div>}
      </div>
    </div>
  </section>;
}
