import { ApiClient, ApiNetworkError, ApiRequestAbortedError } from "@salekhpos/packages-api-client";
import { describe, expect, it, vi } from "vitest";

import { createMemoryReadThroughCache } from "../../src/offline/cache";
import { createInventoryMovementIntent, createMobileOperations, searchProducts } from "../../src/services/mobileOperations";

const organizationId = "11111111-1111-4111-8111-111111111111";
const branchId = "22222222-2222-4222-8222-222222222222";
const productId = "33333333-3333-4333-8333-333333333333";
const operationId = "44444444-4444-4444-8444-444444444444";

function json(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), { status, headers: { "content-type": "application/json" } });
}

function client(fetchImplementation: typeof fetch): ApiClient {
  return new ApiClient({ baseUrl: "https://api.example.test", tokenProvider: { async getAccessToken() { return "signed-access-token"; } }, fetch: fetchImplementation });
}

describe("mobile operations service", () => {
  it("uses authenticated exact backend product paths and bounded paging", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json({ items: [], nextCursor: null }));
    const operations = createMobileOperations(client(fetchMock as unknown as typeof fetch), createMemoryReadThroughCache(), () => 1234);
    const result = await operations.listProducts(organizationId, 50);
    expect(result).toEqual({ value: { items: [], nextCursor: null }, source: "server", storedAt: 1234 });
    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(`https://api.example.test/api/v1/organizations/${organizationId}/products?pageSize=50`);
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer signed-access-token");
  });

  it("falls back only to same-scope read cache after a network failure", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(json({ items: [], nextCursor: null }))
      .mockRejectedValue(new TypeError("offline"));
    const cache = createMemoryReadThroughCache();
    const operations = createMobileOperations(client(fetchMock as unknown as typeof fetch), cache, () => 9000);
    await operations.listStock(organizationId, branchId, 20);
    const cached = await operations.listStock(organizationId, branchId, 20);
    expect(cached.source).toBe("cache");
    await expect(operations.listStock(organizationId, "55555555-5555-4555-8555-555555555555", 20)).rejects.toBeInstanceOf(ApiNetworkError);
  });

  it("does not hide invalid server responses behind cached data", async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(json({ items: [], nextCursor: null }))
      .mockResolvedValueOnce(json({ items: "corrupt", nextCursor: null }));
    const operations = createMobileOperations(client(fetchMock as unknown as typeof fetch), createMemoryReadThroughCache());
    await operations.listProducts(organizationId, 50);
    await expect(operations.listProducts(organizationId, 50)).rejects.toThrow("products.items");
  });

  it("propagates cancellation and does not convert it to offline cache", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation(async (_url, init) => {
      await new Promise((resolve) => setTimeout(resolve, 0));
      if (init?.signal?.aborted === true) throw new DOMException("aborted", "AbortError");
      return json({ items: [], nextCursor: null });
    });
    const controller = new AbortController();
    controller.abort();
    await expect(createMobileOperations(client(fetchMock as unknown as typeof fetch), createMemoryReadThroughCache()).listBranches(organizationId, 10, undefined, controller.signal)).rejects.toBeInstanceOf(ApiRequestAbortedError);
  });

  it("sends stable idempotency and verifies inventory response identity", async () => {
    const occurredAt = "2026-09-14T10:00:00.000Z";
    const response = { id: "55555555-5555-4555-8555-555555555555", branchId, productId, kind: "receipt", quantity: 2.5, reason: "Delivery", occurredAt, recordedAt: "2026-09-14T10:00:01.000Z" };
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json(response, 201));
    const operations = createMobileOperations(client(fetchMock as unknown as typeof fetch), createMemoryReadThroughCache());
    const intent = createInventoryMovementIntent({ organizationId, branchId, productId, kind: "receipt", quantity: "2.5", reason: "Delivery", occurredAt }, operationId);
    expect(await operations.recordInventoryMovement(intent)).toMatchObject(response);
    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(`https://api.example.test/api/v1/organizations/${organizationId}/branches/${branchId}/inventory/movements`);
    expect(new Headers(init?.headers).get("idempotency-key")).toBe(operationId);
    expect(JSON.parse(String(init?.body))).toEqual({ productId, kind: "receipt", quantity: 2.5, reason: "Delivery", occurredAt });
  });

  it("uses the real barcode lookup endpoint and converts only 404 to no result", async () => {
    const product = { id: productId, sku: "SKU.1", name: "Milk", unitCode: "EA", barcode: "12345678", isActive: true, version: 1 };
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValueOnce(json(product)).mockResolvedValueOnce(json({}, 404));
    const operations = createMobileOperations(client(fetchMock as unknown as typeof fetch), createMemoryReadThroughCache());
    await expect(operations.lookupProductByBarcode(organizationId, "12345678")).resolves.toMatchObject(product);
    await expect(operations.lookupProductByBarcode(organizationId, "87654321")).resolves.toBeNull();
    expect(fetchMock.mock.calls[0]?.[0]).toContain("/products/by-barcode/12345678");
  });
});

describe("loaded product search", () => {
  const products = [
    { id: productId, sku: "MILK.1", name: "Whole Milk", unitCode: "EA", barcode: "12345678", isActive: true, version: 1 },
    { id: branchId, sku: "BREAD.1", name: "Bread", unitCode: "EA", barcode: null, isActive: true, version: 1 },
  ] as const;

  it("searches loaded name, SKU, and barcode without inventing a server query", () => {
    expect(searchProducts(products, "milk")).toEqual([products[0]]);
    expect(searchProducts(products, "BREAD")).toEqual([products[1]]);
    expect(searchProducts(products, "3456")).toEqual([products[0]]);
    expect(searchProducts(products, " ")).toBe(products);
  });
});
