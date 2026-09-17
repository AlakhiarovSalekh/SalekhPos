export type AccountingSummary = Readonly<{
  branchId: string;
  from: string;
  to: string;
  currency: string | null;
  completedSales: number;
  salesNet: number;
  salesTax: number;
  salesGross: number;
  completedReturns: number;
  refunds: number;
  netReceipts: number;
  cashIn: number;
  cashOut: number;
  approvedPurchaseOrders: number;
  purchaseCommitments: number;
  closedShifts: number;
  shiftVariance: number;
}>;

export type AccountingJournalItem = Readonly<{
  sourceId: string;
  kind: "sale" | "return" | "sale_void" | "cash_in" | "cash_out" | "purchase_commitment";
  occurredAt: string;
  currency: string;
  netAmount: number | null;
  taxAmount: number | null;
  grossAmount: number;
  cashEffect: number;
}>;

export type AccountingJournalPage = Readonly<{
  items: readonly AccountingJournalItem[];
  nextCursor: string | null;
}>;
