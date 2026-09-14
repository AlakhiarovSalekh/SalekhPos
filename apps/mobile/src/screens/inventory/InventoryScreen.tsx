import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, View } from "react-native";

import type { StockLevel } from "@/api/contracts";
import { useApiClient } from "@/api/ApiContext";
import { AppButton, LoadingSurface, Screen } from "@/components/primitives";
import { CacheNotice, EmptyState, SafeErrorNotice, ScreenHeader, operationStyles } from "@/components/operations";
import { appendPage, firstPageState, type PaginationState } from "@/features/pagination";
import { useLocalization } from "@/localization/LocalizationProvider";
import { mobileReadCache } from "@/offline/cache";
import { canUseInventoryMovement } from "@/permissions/policy";
import { createMobileOperations } from "@/services/mobileOperations";
import { mapSafeError, type SafeAppError } from "@/services/safeError";
import { useSession } from "@/state/SessionContext";
import { useWorkspace } from "@/state/workspace";

const PAGE_SIZE = 50;

export function InventoryScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const { session } = useSession();
  const { workspace } = useWorkspace();
  const client = useApiClient();
  const operations = useMemo(() => createMobileOperations(client, mobileReadCache), [client]);
  const [page, setPage] = useState<PaginationState<StockLevel> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<SafeAppError | null>(null);
  const [cachedAt, setCachedAt] = useState<number | null>(null);

  const load = useCallback(async (after?: string, signal?: AbortSignal) => {
    if (workspace.branch === null) return;
    setLoading(true); setError(null);
    try {
      const result = await operations.listStock(workspace.organizationId, workspace.branch.id, PAGE_SIZE, after, signal);
      setCachedAt((current) => result.source === "cache" ? Math.min(current ?? result.storedAt, result.storedAt) : after === undefined ? null : current);
      setPage((current) => after === undefined || current === null
        ? firstPageState(result.value.items, result.value.nextCursor)
        : appendPage(current, result.value.items, result.value.nextCursor));
    } catch (caught) { const safe = mapSafeError(caught); if (safe.code !== "cancelled") setError(safe); }
    finally { setLoading(false); }
  }, [operations, workspace.branch, workspace.organizationId]);

  useEffect(() => { setPage(null); setCachedAt(null); const controller = new AbortController(); void load(undefined, controller.signal); return () => controller.abort(); }, [load]);

  return <Screen>
    <ScreenHeader title={t("inventory.title")} onBack={() => router.back()} />
    {workspace.branch === null ? <>
      <EmptyState message={t("workspace.branchRequired")} />
      <AppButton onPress={() => router.push("/stores")}>{t("workspace.chooseBranch")}</AppButton>
    </> : <>
      <Text style={operationStyles.muted}>{t("workspace.branch", { name: workspace.branch.name })}</Text>
      {session !== null && canUseInventoryMovement(session.authorization) ? <View style={operationStyles.row}>
        <AppButton onPress={() => router.push("/inventory-receipt")}>{t("inventory.receive")}</AppButton>
        <AppButton onPress={() => router.push("/inventory-adjustment")}>{t("inventory.adjust")}</AppButton>
      </View> : null}
      {cachedAt === null ? null : <CacheNotice storedAt={cachedAt} />}
      {error === null ? null : <SafeErrorNotice error={error} onRetry={() => void load()} />}
      {page === null && loading ? <LoadingSurface /> : null}
      {page?.items.length === 0 ? <EmptyState message={t("inventory.empty")} /> : null}
      {page?.items.map((stock) => <View key={stock.productId} style={operationStyles.card}>
        <Text style={operationStyles.strong}>{stock.name}</Text>
        <Text style={operationStyles.muted}>{stock.sku}</Text>
        <Text style={operationStyles.strong}>{t("inventory.quantity", { quantity: formatQuantity(stock.quantity) })}</Text>
      </View>)}
      {page?.nextCursor === null || page === null ? null : <AppButton disabled={loading} onPress={() => void load(page.nextCursor ?? undefined)}>{t("common.loadMore")}</AppButton>}
    </>}
  </Screen>;
}

export function formatQuantity(value: number): string {
  return value.toLocaleString(undefined, { maximumFractionDigits: 6, useGrouping: false });
}
