import type { SaleSummary, SaleStatus, SaleVoid } from "./types";
import type { ReturnSummary } from "@/features/returns/types";

export function deriveSaleStatus(sale: SaleSummary, returns: readonly ReturnSummary[] | null,
  saleVoid: SaleVoid | null | undefined): SaleStatus {
  if (saleVoid) return "voided";
  if (returns === null || saleVoid === undefined) return "restricted";
  const returned = returns.reduce((total, item) => total + item.amount, 0);
  if (returned <= 0) return "completed";
  return returned >= sale.grandTotal - 0.000001 ? "returned" : "partially-returned";
}

export const statusLabel: Record<SaleStatus, string> = {
  completed: "Completed",
  "partially-returned": "Partially returned",
  returned: "Returned",
  voided: "Voided",
  restricted: "Status restricted"
};
