import { describe, expect, it } from "vitest";
import {
  ManagementContractError,
  parsePurchaseReceiptPage,
} from "../../src/api/managementContracts";

const receipt = {
  id: "55555555-5555-4555-8555-555555555555",
  orderId: "11111111-1111-4111-8111-111111111111",
  branchId: "22222222-2222-4222-8222-222222222222",
  reference: "DOCK-A",
  receivedAt: "2026-09-19T12:02:00.123456Z",
  createdAt: "2026-09-19T12:02:01.000Z",
  receivedBySubject: "owner",
  lines: [{
    productId: "44444444-4444-4444-8444-444444444444",
    quantity: 1,
    movementId: "66666666-6666-4666-8666-666666666666",
  }],
};

describe("management purchase-receipt contracts", () => {
  it("parses a bounded receipt page and UUID cursor", () => {
    const cursor = "77777777-7777-4777-8777-777777777777";
    const page = parsePurchaseReceiptPage({ items: [receipt], nextCursor: cursor });
    expect(page.items).toHaveLength(1);
    expect(page.items[0]?.id).toBe(receipt.id);
    expect(page.items[0]?.lines[0]?.movementId).toBe("66666666-6666-4666-8666-666666666666");
    expect(page.nextCursor).toBe(cursor);
  });

  it("rejects an invalid receipt page cursor", () => {
    expect(() => parsePurchaseReceiptPage({ items: [receipt], nextCursor: "not-a-uuid" }))
      .toThrow(ManagementContractError);
  });
});
