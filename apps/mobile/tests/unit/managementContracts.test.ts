import { describe, expect, it } from "vitest";
import { ManagementContractError, parsePurchaseOrder } from "../../src/api/managementContracts";

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

describe("management purchase-order contracts", () => {
  it("accepts the persisted 100-character reference boundary", () => {
    expect(parsePurchaseOrder(order).reference).toBe("R".repeat(100));
  });

  it("rejects a purchase-order reference beyond the backend contract", () => {
    expect(() => parsePurchaseOrder({ ...order, reference: "R".repeat(101) }))
      .toThrow(ManagementContractError);
  });
});
