"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { getSaleReturnSummaries } from "@/features/returns/api";
import type { ReturnSummary } from "@/features/returns/types";
import { ApiError, formatDate, formatMoney, getPayment, getSaleVoid, shortId } from "../api";
import { deriveSaleStatus } from "../status";
import type { Payment, SaleStatus, SaleSummary, SaleVoid } from "../types";
import { SaleStatusBadge } from "./SaleStatusBadge";

type Sidecar = { payment: Payment | null; status: SaleStatus };

export function SaleRow({ sale, organizationId, branchId, timeZone }: { sale: SaleSummary; organizationId: string; branchId: string; timeZone?: string }) {
  const [sidecar, setSidecar] = useState<Sidecar | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    Promise.allSettled([
      getPayment(organizationId, branchId, sale.id, controller.signal),
      getSaleReturnSummaries(organizationId, branchId, sale.id, controller.signal),
      getSaleVoid(organizationId, branchId, sale.id, controller.signal)
    ]).then(results => {
      if (controller.signal.aborted) return;
      const payment = results[0].status === "fulfilled" ? results[0].value as Payment | null : null;
      const returns = results[1].status === "fulfilled" ? results[1].value as ReturnSummary[] : results[1].reason instanceof ApiError && results[1].reason.kind === "permission" ? null : null;
      const saleVoid = results[2].status === "fulfilled" ? results[2].value as SaleVoid | null : results[2].reason instanceof ApiError && results[2].reason.kind === "permission" ? undefined : undefined;
      setSidecar({ payment, status: deriveSaleStatus(sale, returns, saleVoid) });
    });
    return () => controller.abort();
  }, [branchId, organizationId, sale]);
  const query = `organization=${encodeURIComponent(organizationId)}&branch=${encodeURIComponent(branchId)}`;
  return <tr>
    <td><Link className="record-link" href={`/sales/${sale.id}?${query}`}>#{shortId(sale.id)}</Link><small>{formatDate(sale.completedAt, timeZone)}</small></td>
    <td>{sidecar ? <SaleStatusBadge status={sidecar.status} /> : <span className="skeleton-text">Checking…</span>}</td>
    <td><strong>{formatMoney(sale.grandTotal, sale.currency)}</strong><small>Tax {formatMoney(sale.taxTotal, sale.currency)}</small></td>
    <td>{sidecar?.payment ? <><strong>{sidecar.payment.method}</strong><small>{sidecar.payment.status} · tendered {formatMoney(sidecar.payment.tendered, sidecar.payment.currency)}</small></> : <span className="muted">Restricted or unavailable</span>}</td>
    <td><span className="mono">{sale.registerId ? shortId(sale.registerId) : "Legacy"}</span></td>
  </tr>;
}
