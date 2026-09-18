import { describe, expect, it } from "vitest";

import {
  SalesContractError,
  parseCompletedSale,
  parseSalePage,
} from "../../src/api/salesContracts";

const saleId = "11111111-1111-4111-8111-111111111111";
const branchId = "22222222-2222-4222-8222-222222222222";
const shiftId = "33333333-3333-4333-8333-333333333333";
const registerId = "44444444-4444-4444-8444-444444444444";
const productId = "55555555-5555-4555-8555-555555555555";
const priceId = "66666666-6666-4666-8666-666666666666";

const summary = {
  id: saleId,
  branchId,
  shiftId,
  registerId,
  currency: "GEL",
  netTotal: 10,
  taxTotal: 1.8,
  grandTotal: 11.8,
  cashReceived: 20,
  changeDue: 8.2,
  completedAt: "2026-09-18T12:00:00Z",
};

describe("mobile sales contracts", () => {
  it("accepts a bounded sale page and normalizes timestamps", () => {
    const page = parseSalePage({ items: [summary], nextCursor: saleId });
    expect(page.items).toHaveLength(1);
    expect(page.items[0]?.completedAt).toBe("2026-09-18T12:00:00.000Z");
    expect(page.nextCursor).toBe(saleId);
  });

  it("verifies financial and line arithmetic for full sale details", () => {
    const sale = parseCompletedSale({
      ...summary,
      lines: [{
        lineNumber: 1,
        productId,
        priceId,
        quantity: 1,
        unitAmount: 11.8,
        currency: "GEL",
        taxMode: "inclusive",
        taxRate: 18,
        netAmount: 10,
        taxAmount: 1.8,
        grossAmount: 11.8,
      }],
    });
    expect(sale.lines[0]?.productId).toBe(productId);
    expect(() => parseCompletedSale({ ...sale, grandTotal: 12 })).toThrow(SalesContractError);
  });

  it("rejects cross-currency lines and malformed cursors", () => {
    expect(() => parseCompletedSale({
      ...summary,
      lines: [{
        lineNumber: 1,
        productId,
        priceId,
        quantity: 1,
        unitAmount: 11.8,
        currency: "USD",
        taxMode: "inclusive",
        taxRate: 18,
        netAmount: 10,
        taxAmount: 1.8,
        grossAmount: 11.8,
      }],
    })).toThrow(SalesContractError);
    expect(() => parseSalePage({ items: [], nextCursor: "opaque" })).toThrow(SalesContractError);
  });
});
