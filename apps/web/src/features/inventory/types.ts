export type Session = { configured: boolean; authenticated: boolean; name: string | null; csrfToken: string | null };
export type InventoryBranch = { branchId: string; code: string; name: string; timeZoneId: string; canView: boolean; canAdjust: boolean };
export type InventoryAccess = { organizationId: string; branches: InventoryBranch[] };
export type StockItem = { productId: string; sku: string; name: string; quantity: string };
export type StockPage = { items: StockItem[]; nextCursor: string | null };
export type MovementKind = "receipt" | "adjustment_in" | "adjustment_out";
export type MovementInput = { productId: string; kind: MovementKind; quantity: string; reason: string | null; occurredAt: string };
export type StockMovement = MovementInput & { id: string; branchId: string; recordedAt: string };

const object = (value: unknown): Record<string, unknown> => {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("Invalid server response");
  return value as Record<string, unknown>;
};
const string = (value: unknown) => { if (typeof value !== "string") throw new Error("Invalid server response"); return value; };
const uuid = (value: unknown) => { const text = string(value); if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(text)) throw new Error("Invalid server response"); return text; };
const bool = (value: unknown) => { if (typeof value !== "boolean") throw new Error("Invalid server response"); return value; };
const nullableUuid = (value: unknown) => value === null ? null : uuid(value);

export function parseSession(value: unknown): Session {
  const v = object(value);
  return { configured: bool(v.configured), authenticated: bool(v.authenticated), name: v.name === null ? null : string(v.name), csrfToken: v.csrfToken === null ? null : string(v.csrfToken) };
}
export function parseAccess(value: unknown): InventoryAccess {
  const v = object(value);
  if (!Array.isArray(v.branches)) throw new Error("Invalid server response");
  return { organizationId: uuid(v.organizationId), branches: v.branches.map(entry => { const b = object(entry); return { branchId: uuid(b.branchId), code: string(b.code), name: string(b.name), timeZoneId: string(b.timeZoneId), canView: bool(b.canView), canAdjust: bool(b.canAdjust) }; }) };
}
export function parseStockPage(value: unknown): StockPage {
  const v = object(value);
  if (!Array.isArray(v.items)) throw new Error("Invalid server response");
  return { items: v.items.map(entry => { const item = object(entry); const quantity = item.quantity; if (typeof quantity !== "number" && typeof quantity !== "string") throw new Error("Invalid server response"); return { productId: uuid(item.productId), sku: string(item.sku), name: string(item.name), quantity: String(quantity) }; }), nextCursor: nullableUuid(v.nextCursor) };
}
export function parseMovement(value: unknown): StockMovement {
  const v = object(value); const kind = string(v.kind);
  if (!["receipt", "adjustment_in", "adjustment_out"].includes(kind)) throw new Error("Invalid server response");
  const quantity = v.quantity; if (typeof quantity !== "number" && typeof quantity !== "string") throw new Error("Invalid server response");
  return { id: uuid(v.id), branchId: uuid(v.branchId), productId: uuid(v.productId), kind: kind as MovementKind, quantity: String(quantity), reason: v.reason === null ? null : string(v.reason), occurredAt: string(v.occurredAt), recordedAt: string(v.recordedAt) };
}
