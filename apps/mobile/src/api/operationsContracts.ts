const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu;
const CURRENCY_PATTERN = /^[A-Z]{3}$/u;

export class OperationsContractError extends Error {
  constructor(readonly field: string) {
    super(`The operations API field '${field}' is invalid.`);
    this.name = "OperationsContractError";
  }
}

export type RegisterSummary = Readonly<{
  id: string;
  branchId: string;
  code: string;
  name: string;
  isActive: boolean;
  createdAt: string;
}>;

export type PriceSummary = Readonly<{
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
export type ResolvedPriceSummary = Readonly<Omit<PriceSummary, "id" | "createdAt"> & { priceId: string }>;

export type ShiftSummary = Readonly<{
  id: string;
  branchId: string;
  registerId: string;
  status: string;
  currency: string;
  openingBalance: number;
  openedAt: string;
  openedBy: string;
}>;

export type CashMovementSummary = Readonly<{
  id: string;
  shiftId: string;
  kind: string;
  currency: string;
  amount: number;
  reason: string;
  recordedAt: string;
  recordedBy: string;
}>;

export type ClosedShiftSummary = Readonly<{
  id: string;
  branchId: string;
  registerId: string;
  status: string;
  currency: string;
  openingBalance: number;
  cashSales: number;
  cashRefunds: number;
  cashIn: number;
  cashOut: number;
  expectedCash: number;
  countedCash: number;
  variance: number;
  openedAt: string;
  closedAt: string;
  openedBy: string;
  closedBy: string;
}>;

export type PaymentEventSummary = Readonly<{
  id: string;
  paymentId: string;
  branchId: string;
  kind: "capture" | "return_refund" | "void_refund";
  sourceId: string;
  method: string;
  status: string;
  currency: string;
  amount: number;
  completedAt: string;
}>;

export type CursorPage<T> = Readonly<{ items: readonly T[]; nextCursor: string | null }>;
export type PaymentCursorPage = Readonly<{ items: readonly PaymentEventSummary[]; nextCursor: string | null }>;

function object(value: unknown, field: string): Record<string, unknown> {
  if (value === null || typeof value !== "object" || Array.isArray(value)) throw new OperationsContractError(field);
  return value as Record<string, unknown>;
}

function text(value: unknown, field: string, maximum: number, minimum = 1): string {
  if (typeof value !== "string" || value.length < minimum || value.length > maximum || value.trim() !== value
      || /[\u0000-\u001f\u007f\uD800-\uDFFF]/u.test(value)) throw new OperationsContractError(field);
  return value;
}

function uuid(value: unknown, field: string): string {
  const parsed = text(value, field, 36, 36);
  if (!UUID_PATTERN.test(parsed) || parsed === "00000000-0000-0000-0000-000000000000") throw new OperationsContractError(field);
  return parsed.toLowerCase();
}
function nullableUuid(value: unknown, field: string): string | null {
  return value === null ? null : uuid(value, field);
}

function amount(value: unknown, field: string, minimum = -999_999_999_999_999): number {
  if (typeof value !== "number" || !Number.isFinite(value) || value < minimum || Math.abs(value) > 999_999_999_999_999
      || !Number.isSafeInteger(value * 1_000_000)) throw new OperationsContractError(field);
  return value;
}

function positiveAmount(value: unknown, field: string): number {
  const parsed = amount(value, field, 0);
  if (parsed <= 0) throw new OperationsContractError(field);
  return parsed;
}

function percentage(value: unknown, field: string): number {
  const parsed = amount(value, field, 0);
  if (parsed > 100 || !Number.isSafeInteger(parsed * 10_000)) throw new OperationsContractError(field);
  return parsed;
}

function shiftStatus(value: unknown, field: string, expected: "open" | "closed"): "open" | "closed" {
  const parsed = text(value, field, 32);
  if (parsed !== expected) throw new OperationsContractError(field);
  return expected;
}

function cashKind(value: unknown, field: string): string {
  const parsed = text(value, field, 32);
  if (parsed !== "cash_in" && parsed !== "cash_out") throw new OperationsContractError(field);
  return parsed;
}
function instant(value: unknown, field: string): string {
  const parsed = text(value, field, 64, 10);
  const milliseconds = Date.parse(parsed);
  if (!Number.isFinite(milliseconds) || !/(?:Z|[+-]\d{2}:\d{2})$/u.test(parsed)) throw new OperationsContractError(field);
  return new Date(milliseconds).toISOString();
}

function currency(value: unknown, field: string): string {
  const parsed = text(value, field, 3, 3);
  if (!CURRENCY_PATTERN.test(parsed)) throw new OperationsContractError(field);
  return parsed;
}

function boolean(value: unknown, field: string): boolean {
  if (typeof value !== "boolean") throw new OperationsContractError(field);
  return value;
}

function uuidPage<T>(value: unknown, parser: (item: unknown, field: string) => T, field: string): CursorPage<T> {
  const result = object(value, field);
  if (!Array.isArray(result.items) || result.items.length > 100) throw new OperationsContractError(`${field}.items`);
  const nextCursor = result.nextCursor === null ? null : uuid(result.nextCursor, `${field}.nextCursor`);
  return Object.freeze({
    items: Object.freeze(result.items.map((item, index) => parser(item, `${field}.items[${index}]`))),
    nextCursor,
  });
}
export function parseRegister(value: unknown, field = "register"): RegisterSummary {
  const item = object(value, field);
  return Object.freeze({
    id: uuid(item.id, `${field}.id`),
    branchId: uuid(item.branchId, `${field}.branchId`),
    code: text(item.code, `${field}.code`, 32),
    name: text(item.name, `${field}.name`, 100),
    isActive: boolean(item.isActive, `${field}.isActive`),
    createdAt: instant(item.createdAt, `${field}.createdAt`),
  });
}

export function parseRegisterPage(value: unknown): CursorPage<RegisterSummary> {
  return uuidPage(value, parseRegister, "registers");
}

export function parsePrice(value: unknown, field = "price"): PriceSummary {
  const item = object(value, field);
  const taxMode = item.taxMode;
  if (taxMode !== "inclusive" && taxMode !== "exclusive") throw new OperationsContractError(`${field}.taxMode`);
  const validUntil = item.validUntil === null ? null : instant(item.validUntil, `${field}.validUntil`);
  return Object.freeze({
    id: uuid(item.id, `${field}.id`), productId: uuid(item.productId, `${field}.productId`),
    branchId: nullableUuid(item.branchId, `${field}.branchId`), amount: positiveAmount(item.amount, `${field}.amount`),
    currency: currency(item.currency, `${field}.currency`), taxMode,
    taxRate: percentage(item.taxRate, `${field}.taxRate`), validFrom: instant(item.validFrom, `${field}.validFrom`),
    validUntil, createdAt: instant(item.createdAt, `${field}.createdAt`),
  });
}
export function parseResolvedPrice(value: unknown): ResolvedPriceSummary {
  const item = object(value, "resolvedPrice");
  const taxMode = item.taxMode;
  if (taxMode !== "inclusive" && taxMode !== "exclusive") throw new OperationsContractError("resolvedPrice.taxMode");
  return Object.freeze({
    priceId: uuid(item.priceId, "resolvedPrice.priceId"),
    productId: uuid(item.productId, "resolvedPrice.productId"),
    branchId: nullableUuid(item.branchId, "resolvedPrice.branchId"),
    amount: positiveAmount(item.amount, "resolvedPrice.amount"),
    currency: currency(item.currency, "resolvedPrice.currency"),
    taxMode,
    taxRate: percentage(item.taxRate, "resolvedPrice.taxRate"),
    validFrom: instant(item.validFrom, "resolvedPrice.validFrom"),
    validUntil: item.validUntil === null ? null : instant(item.validUntil, "resolvedPrice.validUntil"),
  });
}

export function parseShift(value: unknown, field = "shift"): ShiftSummary {
  const item = object(value, field);
  return Object.freeze({
    id: uuid(item.id, `${field}.id`), branchId: uuid(item.branchId, `${field}.branchId`),
    registerId: uuid(item.registerId, `${field}.registerId`), status: shiftStatus(item.status, `${field}.status`, "open"),
    currency: currency(item.currency, `${field}.currency`), openingBalance: amount(item.openingBalance, `${field}.openingBalance`, 0),
    openedAt: instant(item.openedAt, `${field}.openedAt`), openedBy: text(item.openedBy, `${field}.openedBy`, 512),
  });
}

export function parseCashMovement(value: unknown, field = "movement"): CashMovementSummary {
  const item = object(value, field);
  return Object.freeze({
    id: uuid(item.id, `${field}.id`), shiftId: uuid(item.shiftId, `${field}.shiftId`),
    kind: cashKind(item.kind, `${field}.kind`), currency: currency(item.currency, `${field}.currency`),
    amount: positiveAmount(item.amount, `${field}.amount`), reason: text(item.reason, `${field}.reason`, 500),
    recordedAt: instant(item.recordedAt, `${field}.recordedAt`), recordedBy: text(item.recordedBy, `${field}.recordedBy`, 512),
  });
}
export function parseCashMovements(value: unknown): readonly CashMovementSummary[] {
  if (!Array.isArray(value) || value.length > 500) throw new OperationsContractError("movements");
  return Object.freeze(value.map((item, index) => parseCashMovement(item, `movements[${index}]`)));
}

export function parseClosedShift(value: unknown, field = "closedShift"): ClosedShiftSummary {
  const item = object(value, field);
  const result = Object.freeze({
    id: uuid(item.id, `${field}.id`), branchId: uuid(item.branchId, `${field}.branchId`),
    registerId: uuid(item.registerId, `${field}.registerId`), status: shiftStatus(item.status, `${field}.status`, "closed"),
    currency: currency(item.currency, `${field}.currency`), openingBalance: amount(item.openingBalance, `${field}.openingBalance`, 0),
    cashSales: amount(item.cashSales, `${field}.cashSales`, 0), cashRefunds: amount(item.cashRefunds, `${field}.cashRefunds`, 0),
    cashIn: amount(item.cashIn, `${field}.cashIn`, 0), cashOut: amount(item.cashOut, `${field}.cashOut`, 0),
    expectedCash: amount(item.expectedCash, `${field}.expectedCash`), countedCash: amount(item.countedCash, `${field}.countedCash`, 0),
    variance: amount(item.variance, `${field}.variance`), openedAt: instant(item.openedAt, `${field}.openedAt`),
    closedAt: instant(item.closedAt, `${field}.closedAt`), openedBy: text(item.openedBy, `${field}.openedBy`, 512),
    closedBy: text(item.closedBy, `${field}.closedBy`, 512),
  });
  if (result.status !== "closed" || Date.parse(result.closedAt) < Date.parse(result.openedAt)) throw new OperationsContractError(field);
  const expected = result.openingBalance + result.cashSales - result.cashRefunds + result.cashIn - result.cashOut;
  if (Math.abs(expected - result.expectedCash) > 0.000001 || Math.abs(result.countedCash - result.expectedCash - result.variance) > 0.000001) {
    throw new OperationsContractError(`${field}.reconciliation`);
  }
  return result;
}

export function parseClosedShiftPage(value: unknown): CursorPage<ClosedShiftSummary> {
  return uuidPage(value, parseClosedShift, "closedShifts");
}
export function parsePaymentEvent(value: unknown, field = "paymentEvent"): PaymentEventSummary {
  const item = object(value, field);
  const kind = item.kind;
  if (kind !== "capture" && kind !== "return_refund" && kind !== "void_refund") throw new OperationsContractError(`${field}.kind`);
  return Object.freeze({
    id: uuid(item.id, `${field}.id`), paymentId: uuid(item.paymentId, `${field}.paymentId`),
    branchId: uuid(item.branchId, `${field}.branchId`), kind, sourceId: uuid(item.sourceId, `${field}.sourceId`),
    method: text(item.method, `${field}.method`, 64), status: text(item.status, `${field}.status`, 64),
    currency: currency(item.currency, `${field}.currency`), amount: positiveAmount(item.amount, `${field}.amount`),
    completedAt: instant(item.completedAt, `${field}.completedAt`),
  });
}

export function parsePaymentEventPage(value: unknown): PaymentCursorPage {
  const result = object(value, "paymentEvents");
  if (!Array.isArray(result.items) || result.items.length > 100) throw new OperationsContractError("paymentEvents.items");
  const nextCursor = result.nextCursor === null ? null : text(result.nextCursor, "paymentEvents.nextCursor", 256);
  return Object.freeze({
    items: Object.freeze(result.items.map((item, index) => parsePaymentEvent(item, `paymentEvents.items[${index}]`))),
    nextCursor,
  });
}
