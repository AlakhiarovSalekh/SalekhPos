import { describe, expect, it } from "vitest";
import {
  ManagementContractError,
  parsePurchaseOrder,
  parsePurchaseReceiptPage,
} from "../../src/api/managementContracts";

const order = {
  id: "11111111-1111-4111-8111-111111111111",
  branchId: "22222222-2222-4222-8222-222222222222",
  supplierId: "33333333-3333-4333-8333-333333333333",
  status: "approved",
  currency: "GEL",
  reference: "R".repeat(100),
  total: 2.5,
  version: 3,
  createdAt: "2026-09-19T12:00:00.000Z",
  updatedAt: "2026-09-19T12:01:00.000Z",
  lines: [{
    productId: "44444444-4444-4444-8444-444444444444",
    quantity: 1,
    unitCost: 2.5,
    lineTotal: 2.5,
  }],
};

const receipt = {
  id: "55555555-5555-4555-8555-555555555555",
  orderId: order.id,
  branchId: order.branchId,
  reference: "DOCK-A",
  receivedAt: "2026-09-19T12:02:00.123456Z",
  createdAt: "2026-09-19T12:02:01.000Z",
  receivedBySubject: "owner",
  lines: [{
    productId: order.lines[0].productId,
    quantity: 1,
    movementId: "66666666-6666-4666-8666-666666666666",
  }],
};

describe("management purchase-order contracts", () => {
  it("accepts the persisted 100-character reference boundary", () => {
    expect(parsePurchaseOrder(order).reference).toBe("R".repeat(100));
  });

  it("rejects a purchase-order reference beyond the backend contract", () => {
    expect(() => parsePurchaseOrder({ ...order, reference: "R".repeat(101) }))
      .toThrow(ManagementContractError);
  });
});

describe("management purchase-receipt contracts", () => {
  it("parses a bounded receipt page and UUID cursor", () => {
    const cursor = "77777777-7777-4777-8777-777777777777";
    const page = parsePurchaseReceiptPage({ items: [receipt], nextCursor: cursor });
    expect(page.items).toHaveLength(1);
    expect(page.items[0]?.id).toBe(receipt.id);
    expect(page.items[0]?.lines[0]?.movementId).toBe(receipt.lines[0].movementId);
    expect(page.nextCursor).toBe(cursor);
  });

  it("rejects an invalid receipt page cursor", () => {
    expect(() => parsePurchaseReceiptPage({ items: [receipt], nextCursor: "not-a-uuid" }))
      .toThrow(ManagementContractError);
  });
});
