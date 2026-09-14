const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu;
const MAX_PAGE_SIZE = 100;

export class ContractParseError extends Error {
  constructor(readonly field: string) {
    super(`The API response field '${field}' is invalid.`);
    this.name = "ContractParseError";
  }
}

export type BranchSummary = Readonly<{
  id: string;
  businessId: string;
  regionId: string | null;
  code: string;
  name: string;
  timeZoneId: string;
}>;

export type Product = Readonly<{
  id: string;
  sku: string;
  name: string;
  unitCode: string;
  barcode: string | null;
  isActive: boolean;
  version: number;
}>;

export type StockLevel = Readonly<{
  productId: string;
  sku: string;
  name: string;
  quantity: number;
}>;

export type StockMovement = Readonly<{
  id: string;
  branchId: string;
  productId: string;
  kind: InventoryMovementKind;
  quantity: number;
  reason: string | null;
  occurredAt: string;
  recordedAt: string;
}>;

export type InventoryMovementKind = "receipt" | "adjustment_in" | "adjustment_out";
export type Page<T> = Readonly<{ items: readonly T[]; nextCursor: string | null }>;

function record(value: unknown, field: string): Record<string, unknown> {
  if (typeof value !== "object" || value === null || Array.isArray(value)) throw new ContractParseError(field);
  return value as Record<string, unknown>;
}

function text(value: unknown, field: string, maximum: number): string {
  if (typeof value !== "string" || value.length === 0 || value.length > maximum || value.trim() !== value || /[\u0000-\u001f\u007f\uD800-\uDFFF]/u.test(value)) {
    throw new ContractParseError(field);
  }
  return value;
}

function uuid(value: unknown, field: string): string {
  const parsed = text(value, field, 36);
  if (!UUID_PATTERN.test(parsed) || parsed === "00000000-0000-0000-0000-000000000000") throw new ContractParseError(field);
  return parsed.toLowerCase();
}

function nullableUuid(value: unknown, field: string): string | null {
  return value === null ? null : uuid(value, field);
}

function nullableText(value: unknown, field: string, maximum: number): string | null {
  return value === null ? null : text(value, field, maximum);
}

function integer(value: unknown, field: string, minimum = 1): number {
  if (typeof value !== "number" || !Number.isSafeInteger(value) || value < minimum) throw new ContractParseError(field);
  return value;
}

function decimal(value: unknown, field: string): number {
  if (typeof value !== "number" || !Number.isFinite(value) || !Number.isSafeInteger(value * 1_000_000)) throw new ContractParseError(field);
  return value;
}

function instant(value: unknown, field: string): string {
  const parsed = text(value, field, 64);
  const milliseconds = Date.parse(parsed);
  if (!Number.isFinite(milliseconds) || !/(?:Z|[+-]\d{2}:\d{2})$/u.test(parsed)) throw new ContractParseError(field);
  return new Date(milliseconds).toISOString();
}

function page<T>(value: unknown, itemParser: (value: unknown, field: string) => T, field: string): Page<T> {
  const parsed = record(value, field);
  if (!Array.isArray(parsed.items) || parsed.items.length > MAX_PAGE_SIZE) throw new ContractParseError(`${field}.items`);
  const nextCursor = parsed.nextCursor === null ? null : uuid(parsed.nextCursor, `${field}.nextCursor`);
  return Object.freeze({
    items: Object.freeze(parsed.items.map((item, index) => itemParser(item, `${field}.items[${index}]`))),
    nextCursor,
  });
}

export function parseBranch(value: unknown, field = "branch"): BranchSummary {
  const item = record(value, field);
  return Object.freeze({
    id: uuid(item.id, `${field}.id`),
    businessId: uuid(item.businessId, `${field}.businessId`),
    regionId: nullableUuid(item.regionId, `${field}.regionId`),
    code: text(item.code, `${field}.code`, 64),
    name: text(item.name, `${field}.name`, 200),
    timeZoneId: text(item.timeZoneId, `${field}.timeZoneId`, 100),
  });
}

export function parseBranchPage(value: unknown): Page<BranchSummary> {
  return page(value, parseBranch, "branches");
}

export function parseProduct(value: unknown, field = "product"): Product {
  const item = record(value, field);
  const barcode = nullableText(item.barcode, `${field}.barcode`, 64);
  if (barcode !== null && !/^\d{4,64}$/u.test(barcode)) throw new ContractParseError(`${field}.barcode`);
  return Object.freeze({
    id: uuid(item.id, `${field}.id`),
    sku: code(item.sku, `${field}.sku`, 64, true),
    name: text(item.name, `${field}.name`, 200),
    unitCode: code(item.unitCode, `${field}.unitCode`, 16, false),
    barcode,
    isActive: typeof item.isActive === "boolean" ? item.isActive : (() => { throw new ContractParseError(`${field}.isActive`); })(),
    version: integer(item.version, `${field}.version`),
  });
}

export function parseProductPage(value: unknown): Page<Product> {
  return page(value, parseProduct, "products");
}

export function parseStockLevel(value: unknown, field = "stock"): StockLevel {
  const item = record(value, field);
  return Object.freeze({
    productId: uuid(item.productId, `${field}.productId`),
    sku: text(item.sku, `${field}.sku`, 64),
    name: text(item.name, `${field}.name`, 200),
    quantity: decimal(item.quantity, `${field}.quantity`),
  });
}

export function parseStockPage(value: unknown): Page<StockLevel> {
  return page(value, parseStockLevel, "stock");
}

export function parseStockMovement(value: unknown): StockMovement {
  const item = record(value, "movement");
  const kind = item.kind;
  if (kind !== "receipt" && kind !== "adjustment_in" && kind !== "adjustment_out") throw new ContractParseError("movement.kind");
  return Object.freeze({
    id: uuid(item.id, "movement.id"),
    branchId: uuid(item.branchId, "movement.branchId"),
    productId: uuid(item.productId, "movement.productId"),
    kind,
    quantity: decimal(item.quantity, "movement.quantity"),
    reason: nullableText(item.reason, "movement.reason", 200),
    occurredAt: instant(item.occurredAt, "movement.occurredAt"),
    recordedAt: instant(item.recordedAt, "movement.recordedAt"),
  });
}

export function validateBarcode(value: string): string {
  if (!/^\d{4,64}$/u.test(value)) throw new ContractParseError("barcode");
  return value;
}

export function validatePageSize(value: number): number {
  if (!Number.isInteger(value) || value < 1 || value > MAX_PAGE_SIZE) throw new ContractParseError("pageSize");
  return value;
}

function code(value: unknown, field: string, maximum: number, allowDot: boolean): string {
  const parsed = text(value, field, maximum);
  const pattern = allowDot ? /^[A-Z0-9_.-]+$/u : /^[A-Z0-9_-]+$/u;
  if (!pattern.test(parsed)) throw new ContractParseError(field);
  return parsed;
}
