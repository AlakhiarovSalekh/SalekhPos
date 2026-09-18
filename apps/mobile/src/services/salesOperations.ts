import {
  assertUuid,
  branchPath,
  type ApiClient,
} from "@salekhpos/packages-api-client";

import {
  parseCompletedSale,
  parseSalePage,
  type CompletedSale,
  type SalePage,
} from "@/api/salesContracts";

function validatePageSize(value: number): number {
  if (!Number.isInteger(value) || value < 1 || value > 100) throw new Error("Page size is invalid.");
  return value;
}

function requireResponse<T>(value: T | undefined): T {
  if (value === undefined) throw new Error("The sales response body is empty.");
  return value;
}

export function createMobileSalesOperations(client: ApiClient) {
  return Object.freeze({
    async listSales(
      organizationId: string,
      branchId: string,
      pageSize: number,
      after?: string,
      signal?: AbortSignal,
    ): Promise<SalePage> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const query: Record<string, string | number> = { pageSize: validatePageSize(pageSize) };
      if (after !== undefined) query.after = assertUuid(after, "after");
      const response = await client.get<unknown>(branchPath(organization, branch, "sales"), {
        query,
        ...(signal === undefined ? {} : { signal }),
      });
      const page = parseSalePage(requireResponse(response));
      if (page.items.some((sale) => sale.branchId !== branch)) throw new Error("The sales response crossed branch scope.");
      return page;
    },

    async readSale(
      organizationId: string,
      branchId: string,
      saleId: string,
      signal?: AbortSignal,
    ): Promise<CompletedSale> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const sale = assertUuid(saleId, "saleId");
      const response = await client.get<unknown>(branchPath(organization, branch, "sales", sale),
        signal === undefined ? {} : { signal });
      const parsed = parseCompletedSale(requireResponse(response));
      if (parsed.id !== sale || parsed.branchId !== branch) throw new Error("The sale response scope is invalid.");
      return parsed;
    },
  });
}

export type MobileSalesOperations = ReturnType<typeof createMobileSalesOperations>;
