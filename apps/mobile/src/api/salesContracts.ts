const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu;
const CURRENCY_PATTERN = /^[A-Z]{3}$/u;

export class SalesContractError extends Error {
  constructor(readonly field: string) {
    super(`The sales API field '${field}' is invalid.`);
    this.name = "SalesContractError";
  }
}

export type SaleSummary = Readonly<{
  id: string;
  branchId: string;
  shiftId: string | null;
  registerId: string | null;
  currency: string;
  netTotal: number;
  taxTotal: number;
  grandTotal: number;
  cashReceived: number;
  changeDue: number;
  completedAt: string;
}>;

export type CompletedSaleLine = Readonly<{
  lineNumber: number;
  productId: string;
  priceId: string;
  quantity: number;
  unitAmount: number;
  currency: string;
  taxMode: "inclusive" | "exclusive";
  taxRate: number;
  netAmount: number;
  taxAmount: number;
  grossAmount: number;
}>;

export type CompletedSale = SaleSummary & Readonly<{ lines: readonly CompletedSaleLine[] }>;
export type SalePage = Readonly<{ items: readonly SaleSummary[]; nextCursor: string | null }>;

function object(value: unknown, field: string): Record<string, unknown> {
  if (value === null || typeof value !== "object" || Array.isArray(value)) throw new SalesContractError(field);
  return value as Record<string, unknown>;
}

function uuid(value: unknown, field: string): string {
  if (typeof value !== "string" || !UUID_PATTERN.test(value)
      || value === "00000000-0000-0000-0000-000000000000") throw new SalesContractError(field);
  return value.toLowerCase();
}

function nullableUuid(value: unknown, field: string): string | null {
  return value === null ? null : uuid(value, field);
}

function currency(value: unknown, field: string): string {
  if (typeof value !== "string" || !CURRENCY_PATTERN.test(value)) throw new SalesContractError(field);
  return value;
}

function decimalPlaces(value: number): number {
  const [coefficient, exponentText] = value.toString().toLowerCase().split("e");
  const fractionLength = coefficient?.split(".")[1]?.length ?? 0;
  const exponent = Number(exponentText ?? "0");
  return Math.max(0, fractionLength - exponent);
}

function amount(value: unknown, field: string, minimum = 0): number {
  if (typeof value !== "number" || !Number.isFinite(value) || value < minimum
      || Math.abs(value) > 999_999_999_999_999 || decimalPlaces(value) > 6
      || !Number.isSafeInteger(Math.round(value * 1_000_000))) {
    throw new SalesContractError(field);
  }
  return value;
}

function instant(value: unknown, field: string): string {
  if (typeof value !== "string" || value.length > 64 || !/(?:Z|[+-]\d{2}:\d{2})$/u.test(value)) {
    throw new SalesContractError(field);
  }
  const parsed = Date.parse(value);
  if (!Number.isFinite(parsed)) throw new SalesContractError(field);
  return new Date(parsed).toISOString();
}

function saleBase(value: unknown, field: string): SaleSummary {
  const item = object(value, field);
  const result = Object.freeze({
    id: uuid(item.id, `${field}.id`),
    branchId: uuid(item.branchId, `${field}.branchId`),
    shiftId: nullableUuid(item.shiftId, `${field}.shiftId`),
    registerId: nullableUuid(item.registerId, `${field}.registerId`),
    currency: currency(item.currency, `${field}.currency`),
    netTotal: amount(item.netTotal, `${field}.netTotal`),
    taxTotal: amount(item.taxTotal, `${field}.taxTotal`),
    grandTotal: amount(item.grandTotal, `${field}.grandTotal`),
    cashReceived: amount(item.cashReceived, `${field}.cashReceived`),
    changeDue: amount(item.changeDue, `${field}.changeDue`),
    completedAt: instant(item.completedAt, `${field}.completedAt`),
  });
  if (Math.abs(result.netTotal + result.taxTotal - result.grandTotal) > 0.000001
      || result.cashReceived + 0.000001 < result.grandTotal
      || Math.abs(result.cashReceived - result.grandTotal - result.changeDue) > 0.000001) {
    throw new SalesContractError(`${field}.totals`);
  }
  return result;
}

export function parseSaleSummary(value: unknown, field = "sale"): SaleSummary {
  return saleBase(value, field);
}

export function parseCompletedSaleLine(value: unknown, field = "line"): CompletedSaleLine {
  const item = object(value, field);
  if (!Number.isInteger(item.lineNumber) || (item.lineNumber as number) < 1 || (item.lineNumber as number) > 500) {
    throw new SalesContractError(`${field}.lineNumber`);
  }
  const taxMode = item.taxMode;
  if (taxMode !== "inclusive" && taxMode !== "exclusive") throw new SalesContractError(`${field}.taxMode`);
  const result = Object.freeze({
    lineNumber: item.lineNumber as number,
    productId: uuid(item.productId, `${field}.productId`),
    priceId: uuid(item.priceId, `${field}.priceId`),
    quantity: amount(item.quantity, `${field}.quantity`, Number.EPSILON),
    unitAmount: amount(item.unitAmount, `${field}.unitAmount`),
    currency: currency(item.currency, `${field}.currency`),
    taxMode,
    taxRate: amount(item.taxRate, `${field}.taxRate`),
    netAmount: amount(item.netAmount, `${field}.netAmount`),
    taxAmount: amount(item.taxAmount, `${field}.taxAmount`),
    grossAmount: amount(item.grossAmount, `${field}.grossAmount`),
  });
  if (result.taxRate > 100 || Math.abs(result.netAmount + result.taxAmount - result.grossAmount) > 0.000001) {
    throw new SalesContractError(`${field}.totals`);
  }
  return result;
}

export function parseCompletedSale(value: unknown): CompletedSale {
  const raw = object(value, "sale");
  const base = saleBase(raw, "sale");
  if (!Array.isArray(raw.lines) || raw.lines.length < 1 || raw.lines.length > 500) {
    throw new SalesContractError("sale.lines");
  }
  const lines = Object.freeze(raw.lines.map((line, index) => parseCompletedSaleLine(line, `sale.lines[${index}]`)));
  if (lines.some((line, index) => line.lineNumber !== index + 1 || line.currency !== base.currency)
      || Math.abs(lines.reduce((sum, line) => sum + line.netAmount, 0) - base.netTotal) > 0.000001
      || Math.abs(lines.reduce((sum, line) => sum + line.taxAmount, 0) - base.taxTotal) > 0.000001
      || Math.abs(lines.reduce((sum, line) => sum + line.grossAmount, 0) - base.grandTotal) > 0.000001) {
    throw new SalesContractError("sale.lines.totals");
  }
  return Object.freeze({ ...base, lines });
}

export function parseSalePage(value: unknown): SalePage {
  const page = object(value, "sales");
  if (!Array.isArray(page.items) || page.items.length > 100) throw new SalesContractError("sales.items");
  return Object.freeze({
    items: Object.freeze(page.items.map((item, index) => parseSaleSummary(item, `sales.items[${index}]`))),
    nextCursor: page.nextCursor === null ? null : uuid(page.nextCursor, "sales.nextCursor"),
  });
}
