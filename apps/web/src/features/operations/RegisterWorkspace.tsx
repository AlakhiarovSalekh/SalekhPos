"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { ManagerShell } from "@/features/sales/components/ManagerShell";
import { formatDate, shortId } from "@/features/sales/api";
import { createRegister, getAllRegisters } from "./api";
import { mutationIntent, type MutationIntent } from "./idempotency";
import { operationErrorMessage } from "./operationError";
import { OperationsScopeSelector } from "./OperationsScopeSelector";
import type { Register, RegisterDraft } from "./types";
import { useOperationsScope } from "./useOperationsScope";

export function RegisterWorkspace() {
  const scope = useOperationsScope();
  const [registers, setRegisters] = useState<readonly Register[]>([]);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const intent = useRef<MutationIntent | null>(null);

  const branchName = useMemo(() => scope.branches.find(item => item.id === scope.branchId)?.name,
    [scope.branches, scope.branchId]);
  useEffect(() => {
    if (!scope.organizationId || !scope.branchId) return;
    const controller = new AbortController();
    getAllRegisters(scope.organizationId, scope.branchId, controller.signal)
      .then(items => { if (!controller.signal.aborted) { setRegisters(items); setError(null); setLoading(false); } })
      .catch(reason => { if (!controller.signal.aborted) { setError(operationErrorMessage(reason)); setLoading(false); } });
    queueMicrotask(() => { if (!controller.signal.aborted) setLoading(true); });
    return () => controller.abort();
  }, [scope.organizationId, scope.branchId]);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!scope.organizationId || !scope.branchId) return;
    const draft: RegisterDraft = { code, name };
    setSaving(true); setError(null);
    try {
      intent.current = mutationIntent([scope.organizationId, scope.branchId, draft], intent.current);
      const created = await createRegister(scope.organizationId, scope.branchId, draft, intent.current.idempotencyKey);
      setRegisters(items => [...items.filter(item => item.id !== created.id), created].sort((a, b) => a.code.localeCompare(b.code)));
      setCode(""); setName(""); intent.current = null;
    } catch (reason) { setError(operationErrorMessage(reason)); }
    finally { setSaving(false); }
  }
  return <ManagerShell organizationId={scope.organizationId || undefined} branchId={scope.branchId || undefined} branchName={branchName}>
    <section className="manager-content operations-page">
      <div className="manager-title"><div><p className="eyebrow">REGISTER CONTROL</p><h1>Checkout registers<span className="accent">.</span></h1>
        <p className="description">Provision named checkout lanes for each branch. Trusted-device enrollment remains a separate security step.</p></div></div>
      <OperationsScopeSelector scope={scope} />
      <div className="operations-grid">
        <form className="operations-card" onSubmit={submit}>
          <h2>Create register</h2>
          <label><span>Register code</span><input value={code} maxLength={32} autoCapitalize="characters"
            onChange={event => { setCode(event.target.value); intent.current = null; }} placeholder="POS-01" required /></label>
          <label><span>Register name</span><input value={name} maxLength={100}
            onChange={event => { setName(event.target.value); intent.current = null; }} placeholder="Front counter 1" required /></label>
          <button className="button compact" disabled={saving || !scope.branchId}>{saving ? "Creating…" : "Create register"}</button>
          <p className="quiet">Creating a register does not authorize a physical device. Device provisioning and cryptographic credentials are still required before protected cash operations.</p>
          {error ? <p role="alert" className="operations-error">{error}</p> : null}
        </form>
        <section className="operations-card"><div className="operations-card-heading"><h2>Branch registers</h2><span>{registers.length}</span></div>
          {loading ? <p role="status" className="quiet">Loading registers…</p> : null}
          {!loading && registers.length === 0 ? <p className="quiet">No registers are configured for this branch.</p> : null}
          <div className="operations-list">{registers.map(register => <article key={register.id} className="operations-list-row">
            <div><strong>{register.name}</strong><span>{register.code} · {shortId(register.id)}</span></div>
            <div className="operations-row-meta"><span className={register.isActive ? "status-ok" : "status-muted"}>{register.isActive ? "Active" : "Inactive"}</span>
              <time dateTime={register.createdAt}>{formatDate(register.createdAt)}</time></div>
          </article>)}</div>
        </section>
      </div>
    </section>
  </ManagerShell>;
}
