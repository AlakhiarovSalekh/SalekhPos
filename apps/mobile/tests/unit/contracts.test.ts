import { describe, expect, it } from "vitest";

import {
  ContractParseError,
  parseBranchPage,
  parseProduct,
  parseProductPage,
  parseStockMovement,
  parseStockPage,
  validateBarcode,
  validatePageSize,
} from "../../src/api/contracts";

const id = "11111111-1111-4111-8111-111111111111";
const id2 = "22222222-2222-4222-8222-222222222222";

describe("mobile API contract parsers", () => {
  it("parses bounded branch, product, and stock pages", () => {
    expect(parseBranchPage({ items: [{ id, businessId: id2, regionId: null, code: "TBS", name: "Tbilisi", timeZoneId: "Asia/Tbilisi" }], nextCursor: id2 }).items[0]?.name).toBe("Tbilisi");
    expect(parseProductPage({ items: [{ id, sku: "SKU.1", name: "Milk", unitCode: "EA", barcode: "1234", isActive: true, version: 1 }], nextCursor: null }).items[0]?.barcode).toBe("1234");
    expect(parseStockPage({ items: [{ productId: id, sku: "SKU.1", name: "Milk", quantity: 1.25 }], nextCursor: null }).items[0]?.quantity).toBe(1.25);
  });

  it("rejects oversized pages, unsafe decimals, and malformed identifiers", () => {
    const product = { id, sku: "SKU.1", name: "Milk", unitCode: "EA", barcode: null, isActive: true, version: 1 };
    expect(() => parseProductPage({ items: Array(101).fill(product), nextCursor: null })).toThrow(ContractParseError);
    expect(() => parseProduct({ ...product, id: "not-a-uuid" })).toThrow(ContractParseError);
    expect(() => parseStockPage({ items: [{ productId: id, sku: "SKU", name: "Name", quantity: Number.MAX_SAFE_INTEGER }], nextCursor: null })).toThrow(ContractParseError);
  });

  it("parses canonical UTC movements and rejects server-only movement kinds", () => {
    const movement = { id, branchId: id2, productId: id, kind: "receipt", quantity: 2, reason: null, occurredAt: "2026-09-14T10:00:00.000Z", recordedAt: "2026-09-14T10:00:01Z" };
    expect(parseStockMovement(movement).recordedAt).toBe("2026-09-14T10:00:01.000Z");
    expect(() => parseStockMovement({ ...movement, kind: "sale" })).toThrow(ContractParseError);
  });

  it.each(["123", "12A4", " 1234", "1".repeat(65)])("rejects invalid barcode %s", (barcode) => {
    expect(() => validateBarcode(barcode)).toThrow(ContractParseError);
  });

  it.each([0, 101, 1.5, Number.NaN])("rejects invalid page size %s", (pageSize) => {
    expect(() => validatePageSize(pageSize)).toThrow(ContractParseError);
  });
});
