import { describe, expect, it } from "vitest";
import {
  OperationsContractError, parseClosedShift, parsePaymentEventPage,
  parsePrice, parseRegisterPage, parseResolvedPrice, parseShift,
} from "../../src/api/operationsContracts";

const id = "11111111-1111-4111-8111-111111111111";
const id2 = "22222222-2222-4222-8222-222222222222";
const id3 = "33333333-3333-4333-8333-333333333333";

describe("manager operations contracts", () => {
  it("accepts bounded register and price contracts", () => {
    expect(parseRegisterPage({ items: [{ id, branchId: id2, code: "POS_01", name: "Front", isActive: true,
      createdAt: "2026-09-15T08:00:00Z" }], nextCursor: null }).items[0]?.code).toBe("POS_01");
    expect(parsePrice({ id, productId: id2, branchId: id3, amount: 4.5, currency: "GEL",
      taxMode: "inclusive", taxRate: 18, validFrom: "2026-09-15T08:00:00Z", validUntil: null,
      createdAt: "2026-09-15T07:00:00Z" }).amount).toBe(4.5);
  });
  it("normalizes timestamps and validates resolved pricing", () => {
    const value = parseResolvedPrice({ priceId: id, productId: id2, branchId: null, amount: 7,
      currency: "USD", taxMode: "exclusive", taxRate: 0, validFrom: "2026-09-15T08:00:00+00:00", validUntil: null });
    expect(value.validFrom).toBe("2026-09-15T08:00:00.000Z");
  });

  it("requires closed-shift reconciliation arithmetic", () => {
    const base = { id, branchId: id2, registerId: id3, status: "closed", currency: "GEL",
      openingBalance: 10, cashSales: 20, cashRefunds: 2, cashIn: 3, cashOut: 1,
      expectedCash: 30, countedCash: 29, variance: -1, openedAt: "2026-09-15T08:00:00Z",
      closedAt: "2026-09-15T10:00:00Z", openedBy: "cashier", closedBy: "manager" };
    expect(parseClosedShift(base).variance).toBe(-1);
    expect(() => parseClosedShift({ ...base, expectedCash: 31 })).toThrow(OperationsContractError);
  });

  it("requires supported payment event kinds and bounded opaque cursor", () => {
    const event = { id, paymentId: id2, branchId: id3, kind: "capture", sourceId: id,
      method: "cash", status: "completed", currency: "GEL", amount: 2, completedAt: "2026-09-15T08:00:00Z" };
    expect(parsePaymentEventPage({ items: [event], nextCursor: "opaque-cursor" }).items).toHaveLength(1);
    expect(() => parsePaymentEventPage({ items: [{ ...event, kind: "unknown" }], nextCursor: null })).toThrow();
  });
});
