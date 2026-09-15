export type Register = Readonly<{
  id: string;
  branchId: string;
  code: string;
  name: string;
  isActive: boolean;
  createdAt: string;
}>;

export type RegisterPage = Readonly<{
  items: readonly Register[];
  nextCursor: string | null;
}>;

export type Price = Readonly<{
  id: string;
  productId: string;
  branchId: string | null;
  amount: number;
  currency: string;
  taxMode: "inclusive" | "exclusive";
  taxRate: number;
  validFrom: string;
  validUntil: string | null;
  createdAt: string;
}>;
export type ResolvedPrice = Readonly<Omit<Price, "id" | "createdAt"> & { priceId: string }>;

export type Shift = Readonly<{
  id: string;
  branchId: string;
  registerId: string;
  status: string;
  currency: string;
  openingBalance: number;
  openedAt: string;
  openedBy: string;
}>;

export type CashMovement = Readonly<{
  id: string;
  shiftId: string;
  kind: string;
  currency: string;
  amount: number;
  reason: string;
  recordedAt: string;
  recordedBy: string;
}>;

export type ClosedShift = Readonly<{
  id: string; branchId: string; registerId: string; status: string; currency: string;
  openingBalance: number; cashSales: number; cashRefunds: number; cashIn: number; cashOut: number;
  expectedCash: number; countedCash: number; variance: number;
  openedAt: string; closedAt: string; openedBy: string; closedBy: string;
}>;

export type ClosedShiftPage = Readonly<{ items: readonly ClosedShift[]; nextCursor: string | null }>;

export type PaymentEvent = Readonly<{
  id: string;
  paymentId: string;
  branchId: string;
  kind: string;
  sourceId: string;
  method: string;
  status: string;
  currency: string;
  amount: number;
  completedAt: string;
}>;

export type PaymentEventPage = Readonly<{
  items: readonly PaymentEvent[];
  nextCursor: string | null;
}>;

export type RegisterDraft = Readonly<{ code: string; name: string }>;
export type PriceDraft = Readonly<{
  productId: string; branchId: string | null; amount: number; currency: string;
  taxMode: "inclusive" | "exclusive"; taxRate: number; validFrom: string; validUntil: string | null;
}>;
