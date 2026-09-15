import { describe, expect, it } from "vitest";
import {
  parseCashMovements, parseClosedShift, parsePaymentEventPage, parsePrice,
  parseRegisterPage, parseResolvedPrice, parseShift,
} from "./parsers";
import { validatePriceDraft, validateRegisterDraft } from "./validation";
import { mutationIntent } from "./idempotency";

const id = "11111111-1111-4111-8111-111111111111";
const id2 = "22222222-2222-4222-8222-222222222222";
const id3 = "33333333-3333-4333-8333-333333333333";

describe("operations response parsers", () => {
  it("parses bounded register pages", () => {
    const page = parseRegisterPage({ items: [{ id, branchId: id2, code: "POS-1", name: "Front", isActive: true,
      createdAt: "2026-09-15T08:00:00Z" }], nextCursor: null });
    expect(page.items[0]?.code).toBe("POS-1");
  });
  it("parses price and resolved price contracts", () => {
    const price = parsePrice({ id, productId: id2, branchId: null, amount: 12.5, currency: "GEL",
      taxMode: "inclusive", taxRate: 18, validFrom: "2026-09-15T08:00:00Z", validUntil: null,
      createdAt: "2026-09-15T07:00:00Z" });
    expect(price.amount).toBe(12.5);
    const resolved = parseResolvedPrice({ priceId: id, productId: id2, branchId: id3, amount: 12.5,
      currency: "GEL", taxMode: "exclusive", taxRate: 0, validFrom: "2026-09-15T08:00:00Z", validUntil: null });
    expect(resolved.branchId).toBe(id3);
  });

  it("rejects malformed currency and oversized collections", () => {
    expect(() => parsePrice({ id, productId: id2, branchId: null, amount: 1, currency: "gel",
      taxMode: "inclusive", taxRate: 18, validFrom: "2026-09-15T08:00:00Z", validUntil: null,
      createdAt: "2026-09-15T08:00:00Z" })).toThrow();
    const register = { id, branchId: id2, code: "POS", name: "Front", isActive: true,
      createdAt: "2026-09-15T08:00:00Z" };
    expect(() => parseRegisterPage({ items: Array(101).fill(register), nextCursor: null })).toThrow();
  });
  it("parses shift, cash movement and payment event evidence", () => {
    expect(parseShift({ id, branchId: id2, registerId: id3, status: "open", currency: "GEL",
      openingBalance: 10, openedAt: "2026-09-15T08:00:00Z", openedBy: "cashier" }).status).toBe("open");
    expect(parseCashMovements([{ id, shiftId: id2, kind: "cash_in", currency: "GEL", amount: 3,
      reason: "Float", recordedAt: "2026-09-15T08:10:00Z", recordedBy: "manager" }])).toHaveLength(1);
    const page = parsePaymentEventPage({ items: [{ id, paymentId: id2, branchId: id3, kind: "capture",
      sourceId: id, method: "cash", status: "completed", currency: "GEL", amount: 4,
      completedAt: "2026-09-15T08:20:00Z" }], nextCursor: "opaque_cursor-1" });
    expect(page.nextCursor).toBe("opaque_cursor-1");
  });

  it("parses closed shift reconciliation fields", () => {
    const shift = parseClosedShift({ id, branchId: id2, registerId: id3, status: "closed", currency: "GEL",
      openingBalance: 10, cashSales: 20, cashRefunds: 2, cashIn: 3, cashOut: 1,
      expectedCash: 30, countedCash: 29, variance: -1, openedAt: "2026-09-15T08:00:00Z",
      closedAt: "2026-09-15T10:00:00Z", openedBy: "cashier", closedBy: "manager" });
    expect(shift.variance).toBe(-1);
  });
});
describe("operations input validation", () => {
  it("enforces register server constraints", () => {
    expect(validateRegisterDraft({ code: "POS_01", name: "Front Desk" })).toBeNull();
    expect(validateRegisterDraft({ code: " bad", name: "Front" })).not.toBeNull();
    expect(validateRegisterDraft({ code: "POS", name: " Front" })).not.toBeNull();
  });

  it("enforces UTC pricing windows and currency", () => {
    const valid = { productId: id, branchId: null, amount: 2.5, currency: "GEL" as const,
      taxMode: "inclusive" as const, taxRate: 18, validFrom: "2026-09-15T08:00:00.000Z", validUntil: null };
    expect(validatePriceDraft(valid)).toBeNull();
    expect(validatePriceDraft({ ...valid, currency: "gel" })).not.toBeNull();
    expect(validatePriceDraft({ ...valid, validUntil: "2026-09-15T07:00:00.000Z" })).not.toBeNull();
  });

  it("keeps one idempotency key for an identical mutation fingerprint", () => {
    const first = mutationIntent(["register", "same"], null, () => id);
    const second = mutationIntent(["register", "same"], first, () => id2);
    expect(second).toBe(first);
    expect(mutationIntent(["register", "changed"], first, () => id2).idempotencyKey).toBe(id2);
  });
});
