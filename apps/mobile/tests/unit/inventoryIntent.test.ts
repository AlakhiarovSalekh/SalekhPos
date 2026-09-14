import { describe, expect, it } from "vitest";
import { createInventoryMovementIntent } from "../../src/services/mobileOperations";

const ids = {
  organizationId: "11111111-1111-4111-8111-111111111111",
  branchId: "22222222-2222-4222-8222-222222222222",
  productId: "33333333-3333-4333-8333-333333333333",
  operationId: "44444444-4444-4444-8444-444444444444",
} as const;

describe("inventory movement intent", () => {
  it("canonicalizes a stable idempotent payload", () => {
    const first = createInventoryMovementIntent({ ...ids, kind: "receipt", quantity: "2.500000", reason: "  Delivery  ", occurredAt: "2026-09-14T10:00:00Z" }, ids.operationId);
    const retry = createInventoryMovementIntent({ ...ids, kind: "receipt", quantity: "2.500000", reason: "  Delivery  ", occurredAt: "2026-09-14T10:00:00Z" }, ids.operationId);
    expect(retry).toEqual(first);
    expect(first).toMatchObject({ operationId: ids.operationId, quantity: 2.5, reason: "Delivery", occurredAt: "2026-09-14T10:00:00.000Z" });
  });

  it.each(["0", "-1", "1.0000001", "1e2", "9007199254.740992", "abc"])("rejects unsafe quantity %s", (quantity) => {
    expect(() => createInventoryMovementIntent({ ...ids, kind: "adjustment_in", quantity }, ids.operationId)).toThrow();
  });

  it("rejects invalid UUIDs, non-UTC time, and unsafe reasons", () => {
    expect(() => createInventoryMovementIntent({ ...ids, productId: "bad", kind: "receipt", quantity: "1" }, ids.operationId)).toThrow();
    expect(() => createInventoryMovementIntent({ ...ids, kind: "receipt", quantity: "1", occurredAt: "2026-09-14T10:00:00+04:00" }, ids.operationId)).toThrow();
    expect(() => createInventoryMovementIntent({ ...ids, kind: "receipt", quantity: "1", reason: "x\ny" }, ids.operationId)).toThrow();
  });
});
