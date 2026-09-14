import { boundedArray, decimal, exactKeys, integer, isoDate, object, optionalUuid, text, uuid } from "@/lib/boundedJson";
import type { Branch, BranchPage, Payment, SaleDetail, SaleLine, SalePage, SaleSummary, SaleVoid } from "./types";

const money = (value: unknown, label: string) => decimal(value, label);
const currency = (value: unknown) => {
  const result = text(value, "currency", 3, 3);
  if (!/^[A-Z]{3}$/.test(result)) throw new Error("Invalid currency");
  return result;
};

function parseBranch(value: unknown): Branch {
  const x = object(value, "branch");
  exactKeys(x, ["id", "businessId", "regionId", "code", "name", "timeZoneId"]);
  return { id: uuid(x.id, "branch id"), businessId: uuid(x.businessId, "business id"), regionId: optionalUuid(x.regionId, "region id"), code: text(x.code, "branch code", 64, 1), name: text(x.name, "branch name", 200, 1), timeZoneId: text(x.timeZoneId, "timezone", 100, 1) };
}

export function parseBranchPage(value: unknown): BranchPage {
  const x = object(value, "branch page");
  exactKeys(x, ["items", "nextCursor"]);
  return { items: boundedArray(x.items, "branches", 100).map(parseBranch), nextCursor: optionalUuid(x.nextCursor, "branch cursor") };
}

function parseSaleSummaryValue(value: unknown): SaleSummary {
  const x = object(value, "sale");
  exactKeys(x, ["id", "branchId", "shiftId", "registerId", "currency", "netTotal", "taxTotal", "grandTotal", "cashReceived", "changeDue", "completedAt"]);
  const result = { id: uuid(x.id, "sale id"), branchId: uuid(x.branchId, "branch id"), shiftId: optionalUuid(x.shiftId, "shift id"), registerId: optionalUuid(x.registerId, "register id"), currency: currency(x.currency), netTotal: money(x.netTotal, "net total"), taxTotal: money(x.taxTotal, "tax total"), grandTotal: money(x.grandTotal, "grand total"), cashReceived: money(x.cashReceived, "cash received"), changeDue: money(x.changeDue, "change due"), completedAt: isoDate(x.completedAt, "completed at") };
  if (result.netTotal + result.taxTotal < 0 || result.changeDue > result.cashReceived) throw new Error("Invalid sale totals");
  return result;
}

function parseSaleLine(value: unknown): SaleLine {
  const x = object(value, "sale line");
  exactKeys(x, ["lineNumber", "productId", "priceId", "quantity", "unitAmount", "currency", "taxMode", "taxRate", "netAmount", "taxAmount", "grossAmount"]);
  const taxMode = text(x.taxMode, "tax mode", 9, 9);
  if (taxMode !== "inclusive" && taxMode !== "exclusive") throw new Error("Invalid tax mode");
  return { lineNumber: integer(x.lineNumber, "line number", 1, 500), productId: uuid(x.productId, "product id"), priceId: uuid(x.priceId, "price id"), quantity: decimal(x.quantity, "quantity", { min: Number.MIN_VALUE }), unitAmount: money(x.unitAmount, "unit amount"), currency: currency(x.currency), taxMode, taxRate: decimal(x.taxRate, "tax rate", { max: 100 }), netAmount: money(x.netAmount, "net amount"), taxAmount: money(x.taxAmount, "tax amount"), grossAmount: money(x.grossAmount, "gross amount") };
}

export function parseSalePage(value: unknown): SalePage {
  const x = object(value, "sale page");
  exactKeys(x, ["items", "nextCursor"]);
  return { items: boundedArray(x.items, "sales", 100).map(parseSaleSummaryValue), nextCursor: optionalUuid(x.nextCursor, "sale cursor") };
}

export function parseSaleDetail(value: unknown): SaleDetail {
  const x = object(value, "sale detail");
  exactKeys(x, ["id", "branchId", "shiftId", "registerId", "currency", "netTotal", "taxTotal", "grandTotal", "cashReceived", "changeDue", "completedAt", "lines"]);
  const summary = { id: x.id, branchId: x.branchId, shiftId: x.shiftId, registerId: x.registerId, currency: x.currency, netTotal: x.netTotal, taxTotal: x.taxTotal, grandTotal: x.grandTotal, cashReceived: x.cashReceived, changeDue: x.changeDue, completedAt: x.completedAt };
  const result = { ...parseSaleSummaryValue(summary), lines: boundedArray(x.lines, "sale lines", 500).map(parseSaleLine) };
  if (result.lines.some((line, index) => line.lineNumber !== index + 1 || line.currency !== result.currency)) throw new Error("Invalid sale lines");
  return result;
}

export function parsePayment(value: unknown): Payment {
  const x = object(value, "payment");
  exactKeys(x, ["id", "saleId", "branchId", "method", "status", "currency", "amount", "tendered", "change", "completedAt"]);
  return { id: uuid(x.id, "payment id"), saleId: uuid(x.saleId, "sale id"), branchId: uuid(x.branchId, "branch id"), method: text(x.method, "payment method", 32, 1), status: text(x.status, "payment status", 32, 1), currency: currency(x.currency), amount: money(x.amount, "payment amount"), tendered: money(x.tendered, "tendered"), change: money(x.change, "change"), completedAt: isoDate(x.completedAt, "payment completed at") };
}

export function parseSaleVoid(value: unknown): SaleVoid {
  const x = object(value, "sale void");
  exactKeys(x, ["id", "saleId", "branchId", "currency", "amount", "reason", "voidedAt", "lines"]);
  const lines = boundedArray(x.lines, "void lines", 500).map(value => {
    const line = object(value, "void line");
    exactKeys(line, ["lineNumber", "productId", "quantity", "inventoryMovementId"]);
    return { lineNumber: integer(line.lineNumber, "line number", 1, 500), productId: uuid(line.productId, "product id"), quantity: decimal(line.quantity, "quantity", { min: Number.MIN_VALUE }), inventoryMovementId: uuid(line.inventoryMovementId, "movement id") };
  });
  return { id: uuid(x.id, "void id"), saleId: uuid(x.saleId, "sale id"), branchId: uuid(x.branchId, "branch id"), currency: currency(x.currency), amount: money(x.amount, "void amount"), reason: text(x.reason, "void reason", 500, 3), voidedAt: isoDate(x.voidedAt, "voided at"), lines };
}
