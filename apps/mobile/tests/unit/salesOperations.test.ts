import { ApiClient } from "@salekhpos/packages-api-client";
import { describe, expect, it, vi } from "vitest";

import { SalesContractError } from "../../src/api/salesContracts";
import { createMobileSalesOperations } from "../../src/services/salesOperations";

const organizationId = "11111111-1111-4111-8111-111111111111";
const branchId = "22222222-2222-4222-8222-222222222222";
const saleId = "33333333-3333-4333-8333-333333333333";
const shiftId = "44444444-4444-4444-8444-444444444444";
const registerId = "55555555-5555-4555-8555-555555555555";
const productId = "66666666-6666-4666-8666-666666666666";
const priceId = "77777777-7777-4777-8777-777777777777";

function json(value: unknown): Response {
  return new Response(JSON.stringify(value), { status: 200, headers: { "content-type": "application/json" } });
}

function client(fetchImplementation: typeof fetch): ApiClient {
  return new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: { async getAccessToken() { return "signed-access-token"; } },
    fetch: fetchImplementation,
  });
}

const summary = {
  id: saleId, branchId, shiftId, registerId, currency: "GEL",
  netTotal: 10, taxTotal: 0, grandTotal: 10, cashReceived: 20, changeDue: 10,
  completedAt: "2026-09-18T12:00:00Z",
};

describe("mobile sales operations", () => {
  it("uses authenticated branch-scoped list and detail endpoints", async () => {
    const detail = {
      ...summary,
      lines: [{
        lineNumber: 1, productId, priceId, quantity: 1, unitAmount: 10, currency: "GEL",
        taxMode: "inclusive", taxRate: 0, netAmount: 10, taxAmount: 0, grossAmount: 10,
      }],
    };
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(json({ items: [summary], nextCursor: null }))
      .mockResolvedValueOnce(json(detail));
    const operations = createMobileSalesOperations(client(fetchMock as unknown as typeof fetch));

    await expect(operations.listSales(organizationId, branchId, 25)).resolves.toMatchObject({ items: [{ id: saleId }] });
    await expect(operations.readSale(organizationId, branchId, saleId)).resolves.toMatchObject({ id: saleId });

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      `https://api.example.test/api/v1/organizations/${organizationId}/branches/${branchId}/sales?pageSize=25`,
    );
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      `https://api.example.test/api/v1/organizations/${organizationId}/branches/${branchId}/sales/${saleId}`,
    );
    expect(new Headers(fetchMock.mock.calls[0]?.[1]?.headers).get("authorization")).toBe("Bearer signed-access-token");
  });

  it("rejects server data that crosses the selected branch boundary", async () => {
    const wrongBranch = "88888888-8888-4888-8888-888888888888";
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json({
      items: [{ ...summary, branchId: wrongBranch }],
      nextCursor: null,
    }));
    const operations = createMobileSalesOperations(client(fetchMock as unknown as typeof fetch));
    await expect(operations.listSales(organizationId, branchId, 25)).rejects.toBeInstanceOf(SalesContractError);
  });

  it("validates bounded page size before making a request", async () => {
    const fetchMock = vi.fn<typeof fetch>();
    const operations = createMobileSalesOperations(client(fetchMock as unknown as typeof fetch));
    await expect(operations.listSales(organizationId, branchId, 101)).rejects.toThrow("Page size is invalid");
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
