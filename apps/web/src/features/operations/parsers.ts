import { boundedArray, decimal, exactKeys, isoDate, object, optionalUuid, text, uuid } from "@/lib/boundedJson";
import type {
  CashMovement, ClosedShift, ClosedShiftPage, PaymentEvent, PaymentEventPage,
  Price, Register, RegisterPage, ResolvedPrice, Shift,
} from "./types";

function currency(value: unknown): string {
  const result = text(value, "currency", 3, 3);
  if (!/^[A-Z]{3}$/.test(result)) throw new Error("Invalid currency");
  return result;
}

function positiveAmount(value: unknown, label: string): number {
  const result = decimal(value, label, { min: 0, max: 100_000_000_000_000 });
  if (result <= 0) throw new Error(`Invalid ${label}`);
  return result;
}

function taxRate(value: unknown): number {
  const result = decimal(value, "tax rate", { min: 0, max: 100 });
  if (!Number.isSafeInteger(result * 10_000)) throw new Error("Invalid tax rate");
  return result;
}

function shiftStatus(value: unknown, expected: "open" | "closed"): "open" | "closed" {
  const result = text(value, "shift status", 32, 1);
  if (result !== expected) throw new Error("Invalid shift status");
  return expected;
}

function cashKind(value: unknown): string {
  const result = text(value, "movement kind", 32, 1);
  if (result !== "cash_in" && result !== "cash_out") throw new Error("Invalid movement kind");
  return result;
}
function paymentKind(value: unknown): "capture" | "return_refund" | "void_refund" {
  if (value !== "capture" && value !== "return_refund" && value !== "void_refund") throw new Error("Invalid payment event kind");
  return value;
}
function cursor(value: unknown): string | null {
  return value === null ? null : uuid(value, "cursor");
}

function mode(value: unknown): "inclusive" | "exclusive" {
  if (value !== "inclusive" && value !== "exclusive") throw new Error("Invalid tax mode");
  return value;
}
export function parseRegister(value: unknown): Register {
  const x = object(value, "register");
  exactKeys(x, ["id", "branchId", "code", "name", "isActive", "createdAt"]);
  if (typeof x.isActive !== "boolean") throw new Error("Invalid register status");
  return {
    id: uuid(x.id, "register id"), branchId: uuid(x.branchId, "branch id"),
    code: text(x.code, "register code", 32, 1), name: text(x.name, "register name", 100, 1),
    isActive: x.isActive, createdAt: isoDate(x.createdAt, "register created at"),
  };
}

export function parseRegisterPage(value: unknown): RegisterPage {
  const x = object(value, "register page");
  exactKeys(x, ["items", "nextCursor"]);
  return { items: boundedArray(x.items, "registers", 100).map(parseRegister), nextCursor: cursor(x.nextCursor) };
}

export function parsePrice(value: unknown): Price {
  const x = object(value, "price");
  exactKeys(x, ["id", "productId", "branchId", "amount", "currency", "taxMode", "taxRate", "validFrom", "validUntil", "createdAt"]);
  return {
    id: uuid(x.id, "price id"), productId: uuid(x.productId, "product id"), branchId: optionalUuid(x.branchId, "branch id"),
    amount: positiveAmount(x.amount, "amount"), currency: currency(x.currency),
    taxMode: mode(x.taxMode), taxRate: taxRate(x.taxRate),
    validFrom: isoDate(x.validFrom, "valid from"), validUntil: x.validUntil === null ? null : isoDate(x.validUntil, "valid until"),
    createdAt: isoDate(x.createdAt, "created at"),
  };
}

export function parseResolvedPrice(value: unknown): ResolvedPrice {
  const x = object(value, "resolved price");
  exactKeys(x, ["priceId", "productId", "branchId", "amount", "currency", "taxMode", "taxRate", "validFrom", "validUntil"]);
  return {
    priceId: uuid(x.priceId, "price id"), productId: uuid(x.productId, "product id"),
    branchId: optionalUuid(x.branchId, "branch id"), amount: positiveAmount(x.amount, "amount"),
    currency: currency(x.currency), taxMode: mode(x.taxMode), taxRate: taxRate(x.taxRate),
    validFrom: isoDate(x.validFrom, "valid from"), validUntil: x.validUntil === null ? null : isoDate(x.validUntil, "valid until"),
  };
}

export function parseShift(value: unknown): Shift {
  const x = object(value, "shift");
  exactKeys(x, ["id", "branchId", "registerId", "status", "currency", "openingBalance", "openedAt", "openedBy"]);
  return {
    id: uuid(x.id, "shift id"), branchId: uuid(x.branchId, "branch id"), registerId: uuid(x.registerId, "register id"),
    status: shiftStatus(x.status, "open"), currency: currency(x.currency),
    openingBalance: decimal(x.openingBalance, "opening balance", { min: 0 }),
    openedAt: isoDate(x.openedAt, "opened at"), openedBy: text(x.openedBy, "opened by", 256, 1),
  };
}

export function parseCashMovement(value: unknown): CashMovement {
  const x = object(value, "cash movement");
  exactKeys(x, ["id", "shiftId", "kind", "currency", "amount", "reason", "recordedAt", "recordedBy"]);
  return {
    id: uuid(x.id, "movement id"), shiftId: uuid(x.shiftId, "shift id"),
    kind: cashKind(x.kind), currency: currency(x.currency),
    amount: positiveAmount(x.amount, "movement amount"), reason: text(x.reason, "movement reason", 500, 1),
    recordedAt: isoDate(x.recordedAt, "recorded at"), recordedBy: text(x.recordedBy, "recorded by", 256, 1),
  };
}

export function parseCashMovements(value: unknown): readonly CashMovement[] {
  return boundedArray(value, "cash movements", 1000).map(parseCashMovement);
}
export function parseClosedShift(value: unknown): ClosedShift {
  const x = object(value, "closed shift");
  exactKeys(x, ["id", "branchId", "registerId", "status", "currency", "openingBalance", "cashSales", "cashRefunds",
    "cashIn", "cashOut", "expectedCash", "countedCash", "variance", "openedAt", "closedAt", "openedBy", "closedBy"]);
  return {
    id: uuid(x.id, "shift id"), branchId: uuid(x.branchId, "branch id"), registerId: uuid(x.registerId, "register id"),
    status: shiftStatus(x.status, "closed"), currency: currency(x.currency),
    openingBalance: decimal(x.openingBalance, "opening balance", { min: 0 }),
    cashSales: decimal(x.cashSales, "cash sales", { min: 0 }), cashRefunds: decimal(x.cashRefunds, "cash refunds", { min: 0 }),
    cashIn: decimal(x.cashIn, "cash in", { min: 0 }), cashOut: decimal(x.cashOut, "cash out", { min: 0 }),
    expectedCash: decimal(x.expectedCash, "expected cash", { min: -100_000_000_000_000 }),
    countedCash: decimal(x.countedCash, "counted cash", { min: 0 }),
    variance: decimal(x.variance, "variance", { min: -100_000_000_000_000 }),
    openedAt: isoDate(x.openedAt, "opened at"), closedAt: isoDate(x.closedAt, "closed at"),
    openedBy: text(x.openedBy, "opened by", 256, 1), closedBy: text(x.closedBy, "closed by", 256, 1),
  };
}

export function parseClosedShiftPage(value: unknown): ClosedShiftPage {
  const x = object(value, "closed shifts"); exactKeys(x, ["items", "nextCursor"]);
  return { items: boundedArray(x.items, "closed shifts", 100).map(parseClosedShift), nextCursor: cursor(x.nextCursor) };
}
export function parsePaymentEvent(value: unknown): PaymentEvent {
  const x = object(value, "payment event");
  exactKeys(x, ["id", "paymentId", "branchId", "kind", "sourceId", "method", "status", "currency", "amount", "completedAt"]);
  return {
    id: uuid(x.id, "event id"), paymentId: uuid(x.paymentId, "payment id"), branchId: uuid(x.branchId, "branch id"),
    kind: paymentKind(x.kind), sourceId: uuid(x.sourceId, "source id"),
    method: text(x.method, "payment method", 32, 1), status: text(x.status, "payment status", 32, 1),
    currency: currency(x.currency), amount: positiveAmount(x.amount, "payment amount"),
    completedAt: isoDate(x.completedAt, "completed at"),
  };
}

export function parsePaymentEventPage(value: unknown): PaymentEventPage {
  const x = object(value, "payment events");
  exactKeys(x, ["items", "nextCursor"]);
  const nextCursor = x.nextCursor === null ? null : text(x.nextCursor, "payment cursor", 512, 1);
  return { items: boundedArray(x.items, "payment events", 100).map(parsePaymentEvent), nextCursor };
}
