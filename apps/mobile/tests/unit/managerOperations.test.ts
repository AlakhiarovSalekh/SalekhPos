import { ApiClient } from "@salekhpos/packages-api-client";
import { describe, expect, it, vi } from "vitest";
import { OperationsContractError } from "../../src/api/operationsContracts";
import { createManagerOperations } from "../../src/services/managerOperations";

const organizationId = "11111111-1111-4111-8111-111111111111";
const branchId = "22222222-2222-4222-8222-222222222222";
const otherBranchId = "33333333-3333-4333-8333-333333333333";
const registerId = "44444444-4444-4444-8444-444444444444";
const productId = "55555555-5555-4555-8555-555555555555";
const operationId = "66666666-6666-4666-8666-666666666666";
const shiftId = "77777777-7777-4777-8777-777777777777";

function json(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), { status, headers: { "content-type": "application/json" } });
}

function client(fetchImplementation: typeof fetch): ApiClient {
  return new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: { async getAccessToken() { return "access-token"; } },
    fetch: fetchImplementation,
  });
}
describe("mobile manager operations", () => {
  it("lists only register records for the requested branch", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json({ items: [{
      id: registerId, branchId, code: "POS_01", name: "Front", isActive: true,
      createdAt: "2026-09-15T08:00:00Z",
    }], nextCursor: null }));
    const operations = createManagerOperations(client(fetchMock as unknown as typeof fetch));
    const result = await operations.listRegisters(organizationId, branchId, 25);
    expect(result.items).toHaveLength(1);
    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(`https://api.example.test/api/v1/organizations/${organizationId}/branches/${branchId}/registers?pageSize=25`);
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer access-token");
  });

  it("rejects register pages that cross branch scope", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json({ items: [{
      id: registerId, branchId: otherBranchId, code: "POS_01", name: "Front", isActive: true,
      createdAt: "2026-09-15T08:00:00Z",
    }], nextCursor: null }));
    const operations = createManagerOperations(client(fetchMock as unknown as typeof fetch));
    await expect(operations.listRegisters(organizationId, branchId)).rejects.toBeInstanceOf(OperationsContractError);
  });
  it("creates registers with stable idempotency and verified response identity", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json({
      id: registerId, branchId, code: "POS_01", name: "Front", isActive: true,
      createdAt: "2026-09-15T08:00:00Z",
    }, 201));
    const operations = createManagerOperations(client(fetchMock as unknown as typeof fetch));
    const result = await operations.createRegister(organizationId, branchId,
      { code: "POS_01", name: "Front" }, operationId);
    expect(result.id).toBe(registerId);
    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(`https://api.example.test/api/v1/organizations/${organizationId}/branches/${branchId}/registers`);
    expect(new Headers(init?.headers).get("idempotency-key")).toBe(operationId);
    expect(JSON.parse(String(init?.body))).toEqual({ code: "POS_01", name: "Front" });
  });

  it("rejects register mutation response identity drift", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json({
      id: registerId, branchId: otherBranchId, code: "POS_01", name: "Front", isActive: true,
      createdAt: "2026-09-15T08:00:00Z",
    }, 201));
    const operations = createManagerOperations(client(fetchMock as unknown as typeof fetch));
    await expect(operations.createRegister(organizationId, branchId,
      { code: "POS_01", name: "Front" }, operationId)).rejects.toBeInstanceOf(OperationsContractError);
  });
  it("schedules prices with exact scope and idempotency", async () => {
    const input = { productId, branchId, amount: 12.5, currency: "GEL", taxMode: "inclusive" as const,
      taxRate: 18, validFrom: "2026-09-15T08:00:00.000Z", validUntil: null };
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json({
      id: registerId, ...input, createdAt: "2026-09-15T07:00:00Z",
    }, 201));
    const operations = createManagerOperations(client(fetchMock as unknown as typeof fetch));
    const result = await operations.schedulePrice(organizationId, input, operationId);
    expect(result.amount).toBe(12.5);
    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(`https://api.example.test/api/v1/organizations/${organizationId}/pricing/prices`);
    expect(new Headers(init?.headers).get("idempotency-key")).toBe(operationId);
  });

  it("rejects resolved prices for another product or branch", async () => {
    const at = "2026-09-15T09:00:00.000Z";
    const response = { priceId: registerId, productId, branchId: otherBranchId, amount: 10,
      currency: "GEL", taxMode: "exclusive", taxRate: 0, validFrom: "2026-09-15T08:00:00Z", validUntil: null };
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json(response));
    const operations = createManagerOperations(client(fetchMock as unknown as typeof fetch));
    await expect(operations.resolvePrice(organizationId, branchId, productId, at))
      .rejects.toBeInstanceOf(OperationsContractError);
  });
  it("converts only 404 open-shift lookup to no result", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValueOnce(json({}, 404)).mockResolvedValueOnce(json({}, 403));
    const operations = createManagerOperations(client(fetchMock as unknown as typeof fetch));
    await expect(operations.readOpenShift(organizationId, branchId, registerId)).resolves.toBeNull();
    await expect(operations.readOpenShift(organizationId, branchId, registerId)).rejects.toThrow();
  });

  it("rejects closed-shift pages that cross branch scope", async () => {
    const value = { id: shiftId, branchId: otherBranchId, registerId, status: "closed", currency: "GEL",
      openingBalance: 10, cashSales: 20, cashRefunds: 2, cashIn: 3, cashOut: 1,
      expectedCash: 30, countedCash: 29, variance: -1, openedAt: "2026-09-15T08:00:00Z",
      closedAt: "2026-09-15T10:00:00Z", openedBy: "cashier", closedBy: "manager" };
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json({ items: [value], nextCursor: null }));
    const operations = createManagerOperations(client(fetchMock as unknown as typeof fetch));
    await expect(operations.listClosedShifts(organizationId, branchId)).rejects.toBeInstanceOf(OperationsContractError);
  });

  it("rejects payment events outside the requested branch", async () => {
    const event = { id: registerId, paymentId: productId, branchId: otherBranchId, kind: "capture",
      sourceId: shiftId, method: "cash", status: "completed", currency: "GEL", amount: 5,
      completedAt: "2026-09-15T09:00:00Z" };
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json({ items: [event], nextCursor: null }));
    const operations = createManagerOperations(client(fetchMock as unknown as typeof fetch));
    await expect(operations.listPaymentEvents(organizationId, branchId)).rejects.toBeInstanceOf(OperationsContractError);
  });
});
