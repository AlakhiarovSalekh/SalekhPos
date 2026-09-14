export type CacheScope = Readonly<{
  organizationId: string;
  branchId?: string;
}>;

export type CacheRecord<T> = Readonly<{
  value: T;
  storedAt: number;
}>;

export interface ReadThroughCache {
  read<T>(scope: CacheScope, resource: string): Promise<CacheRecord<T> | null>;
  write<T>(scope: CacheScope, resource: string, value: T, storedAt: number): Promise<void>;
  removeScope(scope: CacheScope): Promise<void>;
}

function key(scope: CacheScope, resource: string): string {
  return `${scope.organizationId}:${scope.branchId ?? "organization"}:${resource}`;
}

export function createMemoryReadThroughCache(): ReadThroughCache {
  const entries = new Map<string, CacheRecord<unknown>>();
  return Object.freeze({
    async read<T>(scope: CacheScope, resource: string): Promise<CacheRecord<T> | null> {
      return (entries.get(key(scope, resource)) as CacheRecord<T> | undefined) ?? null;
    },
    async write<T>(scope: CacheScope, resource: string, value: T, storedAt: number): Promise<void> {
      entries.set(key(scope, resource), Object.freeze({ value, storedAt }));
    },
    async removeScope(scope: CacheScope): Promise<void> {
      const prefix = `${scope.organizationId}:${scope.branchId ?? "organization"}:`;
      for (const entryKey of entries.keys()) if (entryKey.startsWith(prefix)) entries.delete(entryKey);
    },
  });
}

export const mobileReadCache = createMemoryReadThroughCache();

export type ReadResult<T> = Readonly<{
  value: T;
  source: "server" | "cache";
  storedAt: number;
}>;
