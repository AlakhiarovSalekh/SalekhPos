import { boundedArray, decimal, exactKeys, integer, isoDate, object, text, uuid } from "@/lib/boundedJson";
import type { AccountingJournalItem, AccountingJournalPage, AccountingSummary } from "./types";

const journalKinds = new Set(["sale", "return", "sale_void", "cash_in", "cash_out", "purchase_commitment"]);

function currency(value: unknown): string | null {
  if (value === null) return null;
  const result = text(value, "currency", 3, 3);
  if (!/^[A-Z]{3}$/u.test(result)) throw new Error("Invalid accounting currency");
  return result;
}

function requiredCurrency(value: unknown): string {
  const result = currency(value);
  if (result === null) throw new Error("Accounting currency is required");
  return result;
}

function nullableDecimal(value: unknown, field: string): number | null {
  return value === null ? null : decimal(value, field, { min: -1e15, max: 1e15 });
}

export function parseAccountingSummary(value: unknown): AccountingSummary {
  const x = object(value, "accounting summary");
  exactKeys(x, ["branchId", "from", "to", "currency", "completedSales", "salesNet", "salesTax",
    "salesGross", "completedReturns", "refunds", "netReceipts", "cashIn", "cashOut",
    "approvedPurchaseOrders", "purchaseCommitments", "closedShifts", "shiftVariance"]);
  return {
    branchId: uuid(x.branchId, "branch"),
    from: isoDate(x.from, "from"),
    to: isoDate(x.to, "to"),
    currency: currency(x.currency),
    completedSales: integer(x.completedSales, "completed sales", 0, 1_000_000_000),
    salesNet: decimal(x.salesNet, "sales net", { min: 0 }),
    salesTax: decimal(x.salesTax, "sales tax", { min: 0 }),
    salesGross: decimal(x.salesGross, "sales gross", { min: 0 }),
    completedReturns: integer(x.completedReturns, "completed returns", 0, 1_000_000_000),
    refunds: decimal(x.refunds, "refunds", { min: 0 }),
    netReceipts: decimal(x.netReceipts, "net receipts", { min: -1e15, max: 1e15 }),
    cashIn: decimal(x.cashIn, "cash in", { min: 0 }),
    cashOut: decimal(x.cashOut, "cash out", { min: 0 }),
    approvedPurchaseOrders: integer(x.approvedPurchaseOrders, "purchase orders", 0, 1_000_000_000),
    purchaseCommitments: decimal(x.purchaseCommitments, "purchase commitments", { min: 0 }),
    closedShifts: integer(x.closedShifts, "closed shifts", 0, 1_000_000_000),
    shiftVariance: decimal(x.shiftVariance, "shift variance", { min: -1e15, max: 1e15 }),
  };
}

function parseJournalItem(value: unknown): AccountingJournalItem {
  const x = object(value, "accounting journal item");
  exactKeys(x, ["sourceId", "kind", "occurredAt", "currency", "netAmount", "taxAmount",
    "grossAmount", "cashEffect"]);
  const kind = text(x.kind, "journal kind", 32);
  if (!journalKinds.has(kind)) throw new Error("Invalid accounting journal kind");
  return {
    sourceId: uuid(x.sourceId, "source"),
    kind: kind as AccountingJournalItem["kind"],
    occurredAt: isoDate(x.occurredAt, "occurred at"),
    currency: requiredCurrency(x.currency),
    netAmount: nullableDecimal(x.netAmount, "net amount"),
    taxAmount: nullableDecimal(x.taxAmount, "tax amount"),
    grossAmount: decimal(x.grossAmount, "gross amount", { min: 0 }),
    cashEffect: decimal(x.cashEffect, "cash effect", { min: -1e15, max: 1e15 }),
  };
}

export function parseAccountingJournalPage(value: unknown): AccountingJournalPage {
  const x = object(value, "accounting journal");
  exactKeys(x, ["items", "nextCursor"]);
  const nextCursor = x.nextCursor === null ? null : text(x.nextCursor, "next cursor", 256);
  return {
    items: boundedArray(x.items, "accounting journal items", 100).map(parseJournalItem),
    nextCursor,
  };
}
