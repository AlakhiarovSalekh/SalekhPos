import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Pressable, Text, View } from "react-native";

import type { BranchSummary } from "@/api/contracts";
import { useApiClient } from "@/api/ApiContext";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { CacheNotice, EmptyState, SafeErrorNotice, ScreenHeader, operationStyles } from "@/components/operations";
import { appendPage, firstPageState, type PaginationState } from "@/features/pagination";
import { useLocalization } from "@/localization/LocalizationProvider";
import { mobileReadCache } from "@/offline/cache";
import { createMobileOperations } from "@/services/mobileOperations";
import { mapSafeError, type SafeAppError } from "@/services/safeError";
import { useWorkspace } from "@/state/workspace";

const PAGE_SIZE = 50;

export function StoresScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const client = useApiClient();
  const operations = useMemo(() => createMobileOperations(client, mobileReadCache), [client]);
  const { workspace, selectBranch } = useWorkspace();
  const [page, setPage] = useState<PaginationState<BranchSummary> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<SafeAppError | null>(null);
  const [cachedAt, setCachedAt] = useState<number | null>(null);

  const load = useCallback(async (after?: string, signal?: AbortSignal) => {
    setLoading(true); setError(null);
    try {
      const result = await operations.listBranches(workspace.organizationId, PAGE_SIZE, after, signal);
      setCachedAt((current) => result.source === "cache" ? Math.min(current ?? result.storedAt, result.storedAt) : after === undefined ? null : current);
      setPage((current) => after === undefined || current === null
        ? firstPageState(result.value.items, result.value.nextCursor)
        : appendPage(current, result.value.items, result.value.nextCursor));
    } catch (caught) {
      const safe = mapSafeError(caught);
      if (safe.code !== "cancelled") setError(safe);
    } finally { setLoading(false); }
  }, [operations, workspace.organizationId]);

  useEffect(() => { const controller = new AbortController(); void load(undefined, controller.signal); return () => controller.abort(); }, [load]);

  return <Screen>
    <ScreenHeader title={t("stores.title")} onBack={() => router.back()} />
    <Text style={textStyles.body}>{t("stores.explanation")}</Text>
    {cachedAt === null ? null : <CacheNotice storedAt={cachedAt} />}
    {error === null ? null : <SafeErrorNotice error={error} onRetry={() => void load()} />}
    {page === null && loading ? <LoadingSurface /> : null}
    {page?.items.length === 0 ? <EmptyState message={t("stores.empty")} /> : null}
    {page?.items.map((branch) => <Pressable
      key={branch.id}
      accessibilityRole="button"
      accessibilityState={{ selected: workspace.branch?.id === branch.id }}
      onPress={() => selectBranch(branch)}
      style={operationStyles.card}
    >
      <Text style={operationStyles.strong}>{branch.name}</Text>
      <Text style={operationStyles.muted}>{branch.code} · {branch.timeZoneId}</Text>
      {workspace.branch?.id === branch.id ? <Text style={textStyles.body}>{t("stores.selected")}</Text> : null}
    </Pressable>)}
    {page?.nextCursor === null || page === null ? null : <AppButton disabled={loading} onPress={() => void load(page.nextCursor ?? undefined)}>{t("common.loadMore")}</AppButton>}
  </Screen>;
}
