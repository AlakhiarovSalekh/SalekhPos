import { statusLabel } from "../status";
import type { SaleStatus } from "../types";

export function SaleStatusBadge({ status }: { status: SaleStatus }) {
  return <span className={`status-badge status-${status}`}>{statusLabel[status]}</span>;
}
