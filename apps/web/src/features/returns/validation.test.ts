import { describe, expect, it } from "vitest";
import { validateReturnRequest } from "./api";

const remaining = new Map([["product-a", 2]]);

describe("return validation", () => {
  it("accepts a bounded partial quantity", () => {
    expect(validateReturnRequest({ saleId: "sale", reason: "Customer request", lines: [{ productId: "product-a", quantity: 1.25 }] }, remaining)).toEqual([]);
  });

  it("rejects excessive, over-precision and unselected quantities", () => {
    expect(validateReturnRequest({ saleId: "sale", reason: "x", lines: [] }, remaining)).toHaveLength(2);
    expect(validateReturnRequest({ saleId: "sale", reason: "Valid reason", lines: [{ productId: "product-a", quantity: 3 }] }, remaining)).toHaveLength(1);
    expect(validateReturnRequest({ saleId: "sale", reason: "Valid reason", lines: [{ productId: "product-a", quantity: 0.1234567 }] }, remaining)).toHaveLength(1);
  });
});
