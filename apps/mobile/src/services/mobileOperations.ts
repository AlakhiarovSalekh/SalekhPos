import { randomUUID } from "expo-crypto";
import {
  ApiHttpError,
  ApiNetworkError,
  assertUuid,
  branchPath,
  organizationPath,
  type ApiClient,
} from "@salekhpos/packages-api-client";

import {
  parseBranchPage,
  ContractParseError,
  parseProduct,
  parseProductPage,
  parseStockMovement,
  parseStockPage,
  validateBarcode,
  validatePageSize,
  type BranchSummary,
  type InventoryMovementKind,
  type Page,
  type Product,
  type StockLevel,
  type StockMovement,
} from "@/api/contracts";
import type { ReadResult, ReadThroughCache } from "@/offline/cache";

export type InventoryMovementInput = Readonly<{
  organizationId: string;
  branchId: string;
  productId: string;
  kind: InventoryMovementKind;
  quantity: string;
  reason?: string;
  occurredAt?: string;
}>;

export type InventoryMovementIntent = Readonly<{
  operationId: string;
  organizationId: string;
  branchId: string;
  productId: string;
  kind: InventoryMovementKind;
  quantity: number;
  reason: string | null;
  occurredAt: string;
}>;

function pageQuery(pageSize: number, after?: string): Readonly<Record<string, string | number>> {
  validatePageSize(pageSize);
  return after === undefined
    ? { pageSize }
    : { pageSize, after: assertUuid(after, "after") };
}

function cacheResource(name: string, pageSize: number, after?: string): string {
  return `${name}:${pageSize}:${after ?? "first"}`;
}

async function readThrough<T>(
  request: () => Promise<T>,
  cache: ReadThroughCache,
  scope: Readonly<{ organizationId: string; branchId?: string }>,
  resource: string,
  now: () => number,
): Promise<ReadResult<T>> {
  try {
    const value = await request();
    const storedAt = now();
    try { await cache.write(scope, resource, value, storedAt); } catch { /* A read-cache failure must not discard verified server data. */ }
    return Object.freeze({ value, source: "server", storedAt });
  } catch (error) {
    if (!(error instanceof ApiNetworkError)) throw error;
    const cached = await cache.read<T>(scope, resource);
    if (cached === null) throw error;
    return Object.freeze({ value: cached.value, source: "cache", storedAt: cached.storedAt });
  }
}

function requireResponse<T>(value: T | undefined): T {
  if (value === undefined) throw new ContractParseError("response");
  return value;
}

export function createMobileOperations(
  client: ApiClient,
  cache: ReadThroughCache,
  now: () => number = Date.now,
) {
  return Object.freeze({
    async listBranches(organizationId: string, pageSize: number, after?: string, signal?: AbortSignal): Promise<ReadResult<Page<BranchSummary>>> {
      const organization = assertUuid(organizationId, "organizationId");
      const resource = cacheResource("branches", pageSize, after);
      return readThrough(
        async () => parseBranchPage(requireResponse(await client.get<unknown>(organizationPath(organization, "branches"), { query: pageQuery(pageSize, after), ...(signal === undefined ? {} : { signal }) }))),
        cache,
        { organizationId: organization },
        resource,
        now,
      );
    },

    async listProducts(organizationId: string, pageSize: number, after?: string, signal?: AbortSignal): Promise<ReadResult<Page<Product>>> {
      const organization = assertUuid(organizationId, "organizationId");
      const resource = cacheResource("products", pageSize, after);
      return readThrough(
        async () => parseProductPage(requireResponse(await client.get<unknown>(organizationPath(organization, "products"), { query: pageQuery(pageSize, after), ...(signal === undefined ? {} : { signal }) }))),
        cache,
        { organizationId: organization },
        resource,
        now,
      );
    },

    async lookupProductByBarcode(organizationId: string, barcode: string, signal?: AbortSignal): Promise<Product | null> {
      const organization = assertUuid(organizationId, "organizationId");
      const safeBarcode = validateBarcode(barcode);
      try {
        const response = await client.get<unknown>(organizationPath(organization, "products", "by-barcode", safeBarcode), signal === undefined ? {} : { signal });
        return parseProduct(requireResponse(response));
      } catch (error) {
        if (error instanceof ApiHttpError && error.status === 404) return null;
        throw error;
      }
    },

    async listStock(organizationId: string, branchId: string, pageSize: number, after?: string, signal?: AbortSignal): Promise<ReadResult<Page<StockLevel>>> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const resource = cacheResource("stock", pageSize, after);
      return readThrough(
        async () => parseStockPage(requireResponse(await client.get<unknown>(branchPath(organization, branch, "inventory", "stock"), { query: pageQuery(pageSize, after), ...(signal === undefined ? {} : { signal }) }))),
        cache,
        { organizationId: organization, branchId: branch },
        resource,
        now,
      );
    },

    async recordInventoryMovement(intent: InventoryMovementIntent, signal?: AbortSignal): Promise<StockMovement> {
      const response = await client.post<unknown>(branchPath(intent.organizationId, intent.branchId, "inventory", "movements"), {
        body: {
          productId: intent.productId,
          kind: intent.kind,
          quantity: intent.quantity,
          reason: intent.reason,
          occurredAt: intent.occurredAt,
        },
        idempotencyKey: intent.operationId,
        ...(signal === undefined ? {} : { signal }),
      });
      const movement = parseStockMovement(requireResponse(response));
      if (
        movement.branchId !== intent.branchId || movement.productId !== intent.productId ||
        movement.kind !== intent.kind || movement.quantity !== intent.quantity ||
        movement.reason !== intent.reason || movement.occurredAt !== intent.occurredAt
      ) throw new ContractParseError("movement");
      return movement;
    },
  });
}

export type MobileOperations = ReturnType<typeof createMobileOperations>;

export function createInventoryMovementIntent(input: InventoryMovementInput, operationId = randomUUID()): InventoryMovementIntent {
  if (input.kind !== "receipt" && input.kind !== "adjustment_in" && input.kind !== "adjustment_out") throw new Error("Movement kind is invalid.");
  const quantityText = input.quantity.trim();
  if (!/^(?:0|[1-9]\d{0,13})(?:\.\d{1,6})?$/u.test(quantityText)) throw new Error("Quantity is invalid.");
  const quantity = Number(quantityText);
  if (quantity <= 0 || quantity > 99_999_999_999_999.999999 || !Number.isSafeInteger(quantity * 1_000_000)) throw new Error("Quantity is outside the safe mobile range.");
  const reason = input.reason?.trim() || null;
  if (reason !== null && (reason.length > 200 || /[\u0000-\u001f\u007f]/u.test(reason))) throw new Error("Reason is invalid.");
  const occurredAt = input.occurredAt ?? new Date().toISOString();
  if (!occurredAt.endsWith("Z") || !Number.isFinite(Date.parse(occurredAt))) throw new Error("Movement time must be UTC.");
  return Object.freeze({
    operationId: assertUuid(operationId, "operationId"),
    organizationId: assertUuid(input.organizationId, "organizationId"),
    branchId: assertUuid(input.branchId, "branchId"),
    productId: assertUuid(input.productId, "productId"),
    kind: input.kind,
    quantity,
    reason,
    occurredAt: new Date(occurredAt).toISOString(),
  });
}

export function searchProducts(products: readonly Product[], query: string): readonly Product[] {
  const normalized = query.trim().toLocaleLowerCase();
  if (normalized.length === 0) return products;
  return products.filter((product) =>
    product.name.toLocaleLowerCase().includes(normalized) ||
    product.sku.toLocaleLowerCase().includes(normalized) ||
    product.barcode?.includes(normalized) === true,
  );
}
