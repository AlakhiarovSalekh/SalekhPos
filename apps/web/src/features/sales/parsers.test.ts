import { describe, expect, it } from "vitest";
import { parseSaleDetail, parseSalePage } from "./parsers";

const id = (suffix: string) => `10000000-0000-0000-0000-${suffix.padStart(12, "0")}`;
const summary = { id: id("1"), branchId: id("2"), shiftId: id("3"), registerId: id("4"), currency: "GEL", netTotal: 10, taxTotal: 1.8, grandTotal: 11.8, cashReceived: 12, changeDue: 0.2, completedAt: "2026-09-14T10:00:00Z" };

describe("sale response parsers", () => {
  it("accepts the exact sale page contract", () => {
    expect(parseSalePage({ items: [summary], nextCursor: id("9") }).items[0].grandTotal).toBe(11.8);
  });

  it("rejects unknown fields and oversized pages", () => {
    expect(() => parseSalePage({ items: [], nextCursor: null, secret: "no" })).toThrow();
    expect(() => parseSalePage({ items: Array.from({ length: 101 }, () => summary), nextCursor: null })).toThrow();
  });

  it("validates line ordering and historical tax mode", () => {
    const line = { lineNumber: 1, productId: id("5"), priceId: id("6"), quantity: 1, unitAmount: 11.8, currency: "GEL", taxMode: "inclusive", taxRate: 18, netAmount: 10, taxAmount: 1.8, grossAmount: 11.8 };
    expect(parseSaleDetail({ ...summary, lines: [line] }).lines).toHaveLength(1);
    expect(() => parseSaleDetail({ ...summary, lines: [{ ...line, lineNumber: 2 }] })).toThrow();
    expect(() => parseSaleDetail({ ...summary, lines: [{ ...line, taxMode: "unknown" }] })).toThrow();
  });
});
