import { useLocalSearchParams, useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, View } from "react-native";

import type { Product } from "@/api/contracts";
import { useApiClient } from "@/api/ApiContext";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { CacheNotice, EmptyState, Field, SafeErrorNotice, ScreenHeader, operationStyles } from "@/components/operations";
import { appendPage, firstPageState, type PaginationState } from "@/features/pagination";
import { useLocalization } from "@/localization/LocalizationProvider";
import { mobileReadCache } from "@/offline/cache";
import { createMobileOperations, searchProducts } from "@/services/mobileOperations";
import { mapSafeError, type SafeAppError } from "@/services/safeError";
import { useWorkspace } from "@/state/workspace";

const PAGE_SIZE = 50;

export function ProductsScreen() {
  const router = useRouter();
  const { barcode } = useLocalSearchParams<{ barcode?: string }>();
  const { t } = useLocalization();
  const { workspace } = useWorkspace();
  const client = useApiClient();
  const operations = useMemo(() => createMobileOperations(client, mobileReadCache), [client]);
  const [page, setPage] = useState<PaginationState<Product> | null>(null);
  const [query, setQuery] = useState("");
  const [lookup, setLookup] = useState<Product | null | undefined>(undefined);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<SafeAppError | null>(null);
  const [cachedAt, setCachedAt] = useState<number | null>(null);

  const load = useCallback(async (after?: string, signal?: AbortSignal) => {
    setLoading(true); setError(null);
    try {
      const result = await operations.listProducts(workspace.organizationId, PAGE_SIZE, after, signal);
      setCachedAt((current) => result.source === "cache" ? Math.min(current ?? result.storedAt, result.storedAt) : after === undefined ? null : current);
      setPage((current) => after === undefined || current === null
        ? firstPageState(result.value.items, result.value.nextCursor)
        : appendPage(current, result.value.items, result.value.nextCursor));
    } catch (caught) { const safe = mapSafeError(caught); if (safe.code !== "cancelled") setError(safe); }
    finally { setLoading(false); }
  }, [operations, workspace.organizationId]);

  useEffect(() => { const controller = new AbortController(); void load(undefined, controller.signal); return () => controller.abort(); }, [load]);
  useEffect(() => {
    if (typeof barcode !== "string") { setLookup(undefined); return; }
    const controller = new AbortController(); setLoading(true); setError(null);
    void operations.lookupProductByBarcode(workspace.organizationId, barcode, controller.signal)
      .then(setLookup)
      .catch((caught: unknown) => { const safe = mapSafeError(caught); if (safe.code !== "cancelled") setError(safe); })
      .finally(() => setLoading(false));
    return () => controller.abort();
  }, [barcode, operations, workspace.organizationId]);

  const products = searchProducts(page?.items ?? [], query);
  return <Screen>
    <ScreenHeader title={t("products.title")} onBack={() => router.back()} />
    <View style={operationStyles.row}><AppButton onPress={() => router.push("/scanner")}>{t("products.scan")}</AppButton></View>
    <Field label={t("products.search")} value={query} onChangeText={setQuery} autoCapitalize="none" autoCorrect={false} placeholder={t("products.searchPlaceholder")} />
    {cachedAt === null ? null : <CacheNotice storedAt={cachedAt} />}
    {error === null ? null : <SafeErrorNotice error={error} onRetry={() => void load()} />}
    {lookup === null ? <EmptyState message={t("products.barcodeNotFound")} /> : null}
    {lookup === undefined ? null : lookup === null ? null : <ProductCard product={lookup} scanned />}
    {page === null && loading ? <LoadingSurface /> : null}
    {page !== null && products.length === 0 ? <EmptyState message={query.trim().length === 0 ? t("products.empty") : t("products.noMatches")} /> : null}
    {products.map((product) => <ProductCard key={product.id} product={product} />)}
    {page?.nextCursor === null || page === null ? null : <AppButton disabled={loading} onPress={() => void load(page.nextCursor ?? undefined)}>{t("common.loadMore")}</AppButton>}
  </Screen>;
}

function ProductCard({ product, scanned = false }: Readonly<{ product: Product; scanned?: boolean }>) {
  const { t } = useLocalization();
  return <View style={operationStyles.card}>
    {scanned ? <Text style={operationStyles.muted}>{t("products.scannedResult")}</Text> : null}
    <Text style={operationStyles.strong}>{product.name}</Text>
    <Text style={operationStyles.muted}>{product.sku} · {product.unitCode}</Text>
    <Text style={operationStyles.muted}>{product.barcode ?? t("products.noBarcode")}</Text>
    {!product.isActive ? <Text style={textStyles.body}>{t("products.inactive")}</Text> : null}
  </View>;
}
