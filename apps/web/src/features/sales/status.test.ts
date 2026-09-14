import { describe, expect, it } from "vitest";
import { deriveSaleStatus } from "./status";
import type { SaleSummary, SaleVoid } from "./types";

const sale = { id: "1", branchId: "2", shiftId: null, registerId: null, currency: "GEL", netTotal: 90, taxTotal: 10, grandTotal: 100, cashReceived: 100, changeDue: 0, completedAt: "2026-09-14T10:00:00Z" } satisfies SaleSummary;
const returned = (amount: number) => ({ id: "3", saleId: "1", branchId: "2", currency: "GEL", amount, reason: "Changed mind", completedAt: "2026-09-14T11:00:00Z" });

describe("sale management status", () => {
  it("uses actual return totals", () => {
    expect(deriveSaleStatus(sale, [], null)).toBe("completed");
    expect(deriveSaleStatus(sale, [returned(30)], null)).toBe("partially-returned");
    expect(deriveSaleStatus(sale, [returned(40), returned(60)], null)).toBe("returned");
  });

  it("prioritizes a persisted void and fails closed when reversal access is restricted", () => {
    expect(deriveSaleStatus(sale, [], { id: "v" } as SaleVoid)).toBe("voided");
    expect(deriveSaleStatus(sale, null, null)).toBe("restricted");
    expect(deriveSaleStatus(sale, [], undefined)).toBe("restricted");
  });
});
