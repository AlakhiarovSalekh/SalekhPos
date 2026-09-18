"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { mutationIntent, type MutationIntent } from "@/features/operations/idempotency";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { addSupportDiagnostic, createSupportTicket, getSupportTicket, listSupportTickets, transitionSupportTicket } from "./api";
import type { SupportPriority, SupportStatus, SupportTicket, SupportTicketDetail } from "./types";

const statusOptions: readonly SupportStatus[] = ["open", "in_progress", "waiting_for_customer", "resolved", "closed"];

function nextStatuses(status: SupportStatus): readonly SupportStatus[] {
  switch (status) {
    case "open": return ["in_progress", "closed"];
    case "in_progress": return ["waiting_for_customer", "resolved", "closed"];
    case "waiting_for_customer": return ["in_progress", "resolved", "closed"];
    case "resolved": return ["in_progress", "closed"];
    case "closed": return [];
  }
}

export function SupportWorkspace() {
  const scope = useOperationsScope();
  const [tickets, setTickets] = useState<readonly SupportTicket[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [filter, setFilter] = useState<SupportStatus | "all">("all");
  const [detail, setDetail] = useState<SupportTicketDetail | null>(null);
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [priority, setPriority] = useState<SupportPriority>("normal");
  const [transitionStatus, setTransitionStatus] = useState<SupportStatus | "">("");
  const [transitionNote, setTransitionNote] = useState("");
  const [diagnosticKind, setDiagnosticKind] = useState("log");
  const [diagnosticReference, setDiagnosticReference] = useState("");
  const [diagnosticSha, setDiagnosticSha] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const createIntent = useRef<MutationIntent | null>(null);
  const transitionIntent = useRef<MutationIntent | null>(null);
  const diagnosticIntent = useRef<MutationIntent | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    if (!scope.organizationId) return;
    const page = await listSupportTickets(scope.organizationId, filter === "all" ? null : filter, null, signal);
    if (signal?.aborted) return;
    setTickets(page.items);
    setNextCursor(page.nextCursor);
    setError(null);
  }, [scope.organizationId, filter]);

  useEffect(() => {
    if (!scope.organizationId) return;
    const controller = new AbortController();
    queueMicrotask(() => {
      if (!controller.signal.aborted) void load(controller.signal)
        .catch(() => { if (!controller.signal.aborted) setError("Support tickets could not be loaded."); });
    });
    return () => controller.abort();
  }, [load, scope.organizationId]);

  async function select(ticket: SupportTicket) {
    if (!scope.organizationId) return;
    setBusy(true);
    try {
      const value = await getSupportTicket(scope.organizationId, ticket.id);
      setDetail(value);
      setTransitionStatus(nextStatuses(value.ticket.status)[0] ?? "");
      setError(null);
    } catch { setError("The support ticket could not be loaded."); }
    finally { setBusy(false); }
  }

  async function create() {
    if (!scope.organizationId || !subject.trim() || !description.trim()) return;
    const input = {
      branchId: scope.branchId ?? null,
      subject: subject.trim(),
      description: description.trim(),
      priority,
    };
    createIntent.current = mutationIntent(["support-create", scope.organizationId, input], createIntent.current);
    setBusy(true);
    try {
      const created = await createSupportTicket(scope.organizationId, input, createIntent.current.idempotencyKey);
      createIntent.current = null;
      setSubject(""); setDescription("");
      await load();
      await select(created);
    } catch { setError("The support ticket could not be created. Retrying unchanged input is safe."); }
    finally { setBusy(false); }
  }

  async function transition() {
    if (!scope.organizationId || !detail || !transitionStatus || !transitionNote.trim()) return;
    const input = { status: transitionStatus, expectedVersion: detail.ticket.version, note: transitionNote.trim() };
    transitionIntent.current = mutationIntent(
      ["support-transition", scope.organizationId, detail.ticket.id, input], transitionIntent.current);
    setBusy(true);
    try {
      const updated = await transitionSupportTicket(scope.organizationId, detail.ticket.id,
        input, transitionIntent.current.idempotencyKey);
      transitionIntent.current = null;
      setTransitionNote("");
      await load();
      await select(updated);
    } catch { setError("The ticket transition could not be completed. Refresh before changing the request."); }
    finally { setBusy(false); }
  }

  async function addDiagnostic() {
    if (!scope.organizationId || !detail || !diagnosticKind.trim() || !diagnosticReference.trim()
        || !/^[0-9a-fA-F]{64}$/u.test(diagnosticSha.trim())) return;
    const input = {
      kind: diagnosticKind.trim(),
      reference: diagnosticReference.trim(),
      sha256: diagnosticSha.trim().toLowerCase(),
    };
    diagnosticIntent.current = mutationIntent(
      ["support-diagnostic", scope.organizationId, detail.ticket.id, input], diagnosticIntent.current);
    setBusy(true);
    try {
      await addSupportDiagnostic(scope.organizationId, detail.ticket.id, input,
        diagnosticIntent.current.idempotencyKey);
      diagnosticIntent.current = null;
      setDiagnosticReference(""); setDiagnosticSha("");
      await select(detail.ticket);
    } catch { setError("The diagnostic reference could not be attached. Retrying unchanged input is safe."); }
    finally { setBusy(false); }
  }

  async function more() {
    if (!scope.organizationId || !nextCursor || busy) return;
    setBusy(true);
    try {
      const page = await listSupportTickets(scope.organizationId,
        filter === "all" ? null : filter, nextCursor);
      setTickets(current => [...current, ...page.items]);
      setNextCursor(page.nextCursor);
      setError(null);
    } catch { setError("The next support page could not be loaded."); }
    finally { setBusy(false); }
  }

  const allowed = detail ? nextStatuses(detail.ticket.status) : [];
  return <section className="manager-content">
    <div className="manager-title"><div><span className="eyebrow">SUPPORT</span>
      <h1>Support operations</h1>
      <p>Track store incidents, controlled status transitions and integrity-bound diagnostic references.</p></div>
      <label className="metric-card">Status<select value={filter}
        onChange={event => setFilter(event.target.value as SupportStatus | "all")}>
        <option value="all">All tickets</option>{statusOptions.map(value =>
          <option key={value} value={value}>{value.replaceAll("_", " ")}</option>)}
      </select></label></div>
    <OperationsScopeSelector scope={scope} />
    {error ? <p className="error-banner">{error}</p> : null}
    <div className="split-grid">
      <div className="panel"><h2>Create ticket</h2>
        <p className="muted">{scope.branchId ? "The ticket will be bound to the selected branch." : "No branch selected: the ticket will be organization-wide."}</p>
        <label>Subject<input value={subject} maxLength={200} onChange={event => setSubject(event.target.value)} /></label>
        <label>Description<textarea value={description} maxLength={8000} rows={6}
          onChange={event => setDescription(event.target.value)} /></label>
        <label>Priority<select value={priority} onChange={event => setPriority(event.target.value as SupportPriority)}>
          {(["low","normal","high","urgent"] as const).map(value => <option key={value} value={value}>{value}</option>)}
        </select></label>
        <button disabled={busy || !scope.organizationId || !subject.trim() || !description.trim()}
          onClick={() => void create()}>Create support ticket</button>
        <h2>Tickets</h2>
        <div className="data-list">{tickets.map(ticket => <button className="data-list-item" key={ticket.id}
          onClick={() => void select(ticket)}>
          <strong>{ticket.subject}</strong>
          <span>{ticket.priority} · {ticket.status.replaceAll("_", " ")} · v{ticket.version}</span>
          <span>{new Date(ticket.updatedAt).toLocaleString()} · {ticket.id.slice(0,8)}</span>
        </button>)}</div>
        {nextCursor ? <button className="secondary-button" disabled={busy}
          onClick={() => void more()}>{busy ? "Loading…" : "Load more tickets"}</button> : null}
      </div>
      <div className="panel"><h2>Ticket detail</h2>
        {!detail ? <p className="muted">Choose a ticket to inspect its workflow and diagnostics.</p> : <>
          <p><strong>{detail.ticket.subject}</strong></p>
          <p>{detail.ticket.description}</p>
          <p className="muted">{detail.ticket.priority} · {detail.ticket.status.replaceAll("_", " ")}
            {" · "}version {detail.ticket.version} · opened by {detail.ticket.openedBySubject}</p>
          {allowed.length ? <>
            <h3>Transition</h3>
            <label>Next status<select value={transitionStatus}
              onChange={event => setTransitionStatus(event.target.value as SupportStatus)}>
              {allowed.map(value => <option key={value} value={value}>{value.replaceAll("_", " ")}</option>)}
            </select></label>
            <label>Transition note<textarea value={transitionNote} maxLength={2000} rows={3}
              onChange={event => setTransitionNote(event.target.value)} /></label>
            <button disabled={busy || !transitionStatus || !transitionNote.trim()}
              onClick={() => void transition()}>Apply transition</button>
          </> : <p className="muted">This ticket is closed and terminal.</p>}
          <h3>Diagnostics</h3>
          <div className="data-list">{detail.diagnostics.map(item => <article key={item.id}>
            <strong>{item.kind}</strong><span>{item.reference}</span>
            <code>{item.sha256}</code><span>{new Date(item.createdAt).toLocaleString()}</span>
          </article>)}</div>
          <label>Kind<input value={diagnosticKind} maxLength={64}
            onChange={event => setDiagnosticKind(event.target.value)} /></label>
          <label>Reference<input value={diagnosticReference} maxLength={512}
            onChange={event => setDiagnosticReference(event.target.value)} /></label>
          <label>SHA-256<input value={diagnosticSha} maxLength={64} spellCheck={false}
            onChange={event => setDiagnosticSha(event.target.value)} /></label>
          <button disabled={busy || !diagnosticKind.trim() || !diagnosticReference.trim()
              || !/^[0-9a-fA-F]{64}$/u.test(diagnosticSha.trim())}
            onClick={() => void addDiagnostic()}>Attach diagnostic reference</button>
        </>}
      </div>
    </div>
  </section>;
}
