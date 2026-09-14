import { boundedArray, decimal, exactKeys, integer, isoDate, object, optionalUuid, text, uuid } from "@/lib/boundedJson";
import type { Refund, ReturnDetail, ReturnPage, ReturnSummary, ReturnedLine } from "./types";

const currency = (value: unknown) => {
  const result = text(value, "currency", 3, 3);
  if (!/^[A-Z]{3}$/.test(result)) throw new Error("Invalid currency");
  return result;
};

function parseSummary(value: unknown): ReturnSummary {
  const x = object(value, "return");
  exactKeys(x, ["id", "saleId", "branchId", "currency", "amount", "reason", "completedAt"]);
  return { id: uuid(x.id, "return id"), saleId: uuid(x.saleId, "sale id"), branchId: uuid(x.branchId, "branch id"), currency: currency(x.currency), amount: decimal(x.amount, "return amount", { min: Number.MIN_VALUE }), reason: text(x.reason, "return reason", 500, 3), completedAt: isoDate(x.completedAt, "return completed at") };
}

function parseLine(value: unknown): ReturnedLine {
  const x = object(value, "returned line");
  exactKeys(x, ["lineNumber", "productId", "quantity", "amount"]);
  return { lineNumber: integer(x.lineNumber, "line number", 1, 500), productId: uuid(x.productId, "product id"), quantity: decimal(x.quantity, "quantity", { min: Number.MIN_VALUE }), amount: decimal(x.amount, "amount", { min: Number.MIN_VALUE }) };
}

export function parseReturnPage(value: unknown): ReturnPage {
  const x = object(value, "return page");
  exactKeys(x, ["items", "nextCursor"]);
  return { items: boundedArray(x.items, "returns", 100).map(parseSummary), nextCursor: optionalUuid(x.nextCursor, "return cursor") };
}

export function parseReturnDetail(value: unknown): ReturnDetail {
  const x = object(value, "return detail");
  exactKeys(x, ["id", "saleId", "branchId", "currency", "amount", "reason", "completedAt", "lines"]);
  const summary = { id: x.id, saleId: x.saleId, branchId: x.branchId, currency: x.currency, amount: x.amount, reason: x.reason, completedAt: x.completedAt };
  const result = { ...parseSummary(summary), lines: boundedArray(x.lines, "returned lines", 500).map(parseLine) };
  if (result.lines.some((line, index) => line.lineNumber !== index + 1)) throw new Error("Invalid returned lines");
  return result;
}

export function parseRefund(value: unknown): Refund {
  const x = object(value, "refund");
  exactKeys(x, ["id", "paymentId", "branchId", "sourceKind", "sourceId", "method", "status", "currency", "amount", "completedAt"]);
  return { id: uuid(x.id, "refund id"), paymentId: uuid(x.paymentId, "payment id"), branchId: uuid(x.branchId, "branch id"), sourceKind: text(x.sourceKind, "refund source", 32, 1), sourceId: uuid(x.sourceId, "source id"), method: text(x.method, "refund method", 32, 1), status: text(x.status, "refund status", 32, 1), currency: currency(x.currency), amount: decimal(x.amount, "refund amount", { min: Number.MIN_VALUE }), completedAt: isoDate(x.completedAt, "refund completed at") };
}
