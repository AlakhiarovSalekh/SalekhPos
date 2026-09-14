export type Branch = { id: string; businessId: string; regionId: string | null; code: string; name: string; timeZoneId: string };
export type BranchPage = { items: Branch[]; nextCursor: string | null };

export type SaleSummary = {
  id: string; branchId: string; shiftId: string | null; registerId: string | null; currency: string;
  netTotal: number; taxTotal: number; grandTotal: number; cashReceived: number; changeDue: number; completedAt: string;
};
export type SaleLine = {
  lineNumber: number; productId: string; priceId: string; quantity: number; unitAmount: number; currency: string;
  taxMode: "inclusive" | "exclusive"; taxRate: number; netAmount: number; taxAmount: number; grossAmount: number;
};
export type SaleDetail = SaleSummary & { lines: SaleLine[] };
export type SalePage = { items: SaleSummary[]; nextCursor: string | null };
export type Payment = {
  id: string; saleId: string; branchId: string; method: string; status: string; currency: string; amount: number;
  tendered: number; change: number; completedAt: string;
};
export type SaleVoid = {
  id: string; saleId: string; branchId: string; currency: string; amount: number; reason: string; voidedAt: string;
  lines: { lineNumber: number; productId: string; quantity: number; inventoryMovementId: string }[];
};
export type SaleStatus = "completed" | "partially-returned" | "returned" | "voided" | "restricted";
export type Capability = "allowed" | "denied";
