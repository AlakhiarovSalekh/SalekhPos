"use client";

import { useEffect, useMemo, useState } from "react";
import { getCashMovements, getClosedShifts, getOpenShift, getPaymentEvents, getAllRegisters } from "./api";
import { operationErrorMessage } from "./operationError";
import { OperationsScopeSelector } from "./OperationsScopeSelector";
import { useOperationsScope } from "./useOperationsScope";
import type { CashMovement, ClosedShift, PaymentEvent, Register, Shift } from "./types";
import { formatDate, formatMoney } from "@/features/sales/api";

export function ReconciliationWorkspace() {
  const scope = useOperationsScope();
  const [registers, setRegisters] = useState<readonly Register[]>([]);
  const [openShifts, setOpenShifts] = useState<readonly Shift[]>([]);
  const [closedShifts, setClosedShifts] = useState<readonly ClosedShift[]>([]);
  const [payments, setPayments] = useState<readonly PaymentEvent[]>([]);
  const [movements, setMovements] = useState<Record<string, readonly CashMovement[]>>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const ready = Boolean(scope.organizationId && scope.branchId);
  const totals = useMemo(() => payments.reduce((sum, event) => sum + event.amount, 0), [payments]);

  useEffect(() => {
    if (!ready) return;
    const controller = new AbortController();
    queueMicrotask(() => { if (!controller.signal.aborted) { setLoading(true); setError(""); } });
    Promise.all([
      getAllRegisters(scope.organizationId!, scope.branchId!, controller.signal),
      getClosedShifts(scope.organizationId!, scope.branchId!, undefined, controller.signal),
      getPaymentEvents(scope.organizationId!, scope.branchId!, undefined, controller.signal),
    ]).then(async ([registerList, closed, paymentPage]) => {
      if (controller.signal.aborted) return;
      setRegisters(registerList); setClosedShifts(closed.items); setPayments(paymentPage.items);
      const open = (await Promise.all(registerList.map(register =>
        getOpenShift(scope.organizationId!, scope.branchId!, register.id, controller.signal)))).filter(Boolean) as Shift[];
      if (!controller.signal.aborted) { setOpenShifts(open); setError(""); }
    }).catch(errorValue => { if (!controller.signal.aborted) setError(operationErrorMessage(errorValue)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [ready, scope.organizationId, scope.branchId]);

  async function loadMovements(shift: Shift) {
    if (!scope.organizationId || !scope.branchId || movements[shift.id]) return;
    try {
      const result = await getCashMovements(scope.organizationId, scope.branchId, shift.id);
      setMovements(current => ({ ...current, [shift.id]: result }));
    } catch (errorValue) { setError(operationErrorMessage(errorValue)); }
  }

  return <>
    <OperationsScopeSelector scope={scope} />
    {error && <p className="inline-error" role="alert">{error}</p>}
    {!ready ? <section className="state-panel"><h2>Select a branch</h2><p>Reconciliation data is branch-scoped.</p></section> : loading ?
      <section className="state-panel"><div className="spinner" /><p>Loading operational records…</p></section> : <>
      <section className="metric-grid">
        <div><span>Registers</span><strong>{registers.length}</strong></div>
        <div><span>Open shifts</span><strong>{openShifts.length}</strong></div>
        <div><span>Closed shifts</span><strong>{closedShifts.length}</strong></div>
        <div><span>Payment events</span><strong>{formatMoney(totals, payments[0]?.currency ?? "USD")}</strong></div>
      </section>
      <section className="warning-panel"><h4>Trusted-terminal boundary</h4><p>Web management is read-only for shift opening, cash movements and shift closing. Those mutations remain device-signed terminal operations.</p></section>
      <section className="data-card"><div className="card-heading"><div><h2>Open shifts</h2><p>Current register sessions and their cash movement history.</p></div></div>
        {openShifts.length === 0 ? <div className="padded"><p className="muted">No open shifts were found.</p></div> : <div className="table-scroll"><table><thead><tr><th>Register</th><th>Opened</th><th>Balance</th><th>Operator</th><th>Cash history</th></tr></thead><tbody>
          {openShifts.map(shift => <tr key={shift.id}><td className="mono">{shift.registerId.slice(0, 8)}</td><td>{formatDate(shift.openedAt)}</td><td>{formatMoney(shift.openingBalance, shift.currency)}</td><td>{shift.openedBy}</td><td><button className="secondary-button" onClick={() => void loadMovements(shift)}>{movements[shift.id] ? `${movements[shift.id].length} movements` : "Load history"}</button></td></tr>)}
        </tbody></table></div>}
      </section>
      {Object.entries(movements).map(([shiftId, items]) => <section className="data-card" key={shiftId}><div className="card-heading"><div><h2>Cash movements</h2><p>Shift {shiftId.slice(0, 8).toUpperCase()}</p></div></div><div className="table-scroll"><table><thead><tr><th>Kind</th><th>Amount</th><th>Reason</th><th>Recorded</th><th>Operator</th></tr></thead><tbody>{items.map(item => <tr key={item.id}><td>{item.kind}</td><td>{formatMoney(item.amount, item.currency)}</td><td>{item.reason}</td><td>{formatDate(item.recordedAt)}</td><td>{item.recordedBy}</td></tr>)}</tbody></table></div></section>)}
      <section className="data-card"><div className="card-heading"><div><h2>Closed shifts</h2><p>Recent reconciled register sessions.</p></div></div><div className="table-scroll"><table><thead><tr><th>Register</th><th>Closed</th><th>Expected</th><th>Counted</th><th>Variance</th></tr></thead><tbody>{closedShifts.map(shift => <tr key={shift.id}><td className="mono">{shift.registerId.slice(0, 8)}</td><td>{formatDate(shift.closedAt)}</td><td>{formatMoney(shift.expectedCash, shift.currency)}</td><td>{formatMoney(shift.countedCash, shift.currency)}</td><td>{formatMoney(shift.variance, shift.currency)}</td></tr>)}</tbody></table></div></section>
      <section className="data-card"><div className="card-heading"><div><h2>Payment events</h2><p>Recent payment and refund events for reconciliation.</p></div></div><div className="table-scroll"><table><thead><tr><th>Kind</th><th>Method</th><th>Status</th><th>Amount</th><th>Completed</th></tr></thead><tbody>{payments.map(event => <tr key={event.id}><td>{event.kind}</td><td>{event.method}</td><td>{event.status}</td><td>{formatMoney(event.amount, event.currency)}</td><td>{formatDate(event.completedAt)}</td></tr>)}</tbody></table></div></section>
    </>}
  </>;
}
