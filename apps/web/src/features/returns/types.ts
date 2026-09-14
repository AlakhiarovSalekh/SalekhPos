export type ReturnSummary = {
  id: string; saleId: string; branchId: string; currency: string; amount: number; reason: string; completedAt: string;
};
export type ReturnedLine = { lineNumber: number; productId: string; quantity: number; amount: number };
export type ReturnDetail = ReturnSummary & { lines: ReturnedLine[] };
export type ReturnPage = { items: ReturnSummary[]; nextCursor: string | null };
export type Refund = {
  id: string; paymentId: string; branchId: string; sourceKind: string; sourceId: string; method: string;
  status: string; currency: string; amount: number; completedAt: string;
};
export type ReturnLineInput = { productId: string; quantity: number };
export type ReturnRequest = { saleId: string; reason: string; lines: ReturnLineInput[] };
