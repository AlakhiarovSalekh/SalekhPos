import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Pressable, Text, View } from "react-native";

import type { CompletedSale, SaleSummary } from "@/api/salesContracts";
import { useApiClient } from "@/api/ApiContext";
import { AppButton, LoadingSurface, textStyles } from "@/components/primitives";
import { EmptyState, SafeErrorNotice, ScreenHeader, operationStyles } from "@/components/operations";
import { appendPage, firstPageState, type PaginationState } from "@/features/pagination";
import { useLocalization } from "@/localization/LocalizationProvider";
import { createMobileSalesOperations } from "@/services/salesOperations";
import { mapSafeError, type SafeAppError } from "@/services/safeError";
import { useWorkspace } from "@/state/workspace";
import { Screen } from "@/components/primitives";

const PAGE_SIZE = 25;

export function SalesScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const { workspace } = useWorkspace();
  const client = useApiClient();
  const sales = useMemo(() => createMobileSalesOperations(client), [client]);
  const [page, setPage] = useState<PaginationState<SaleSummary> | null>(null);
  const [selected, setSelected] = useState<CompletedSale | null>(null);
  const [loading, setLoading] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [error, setError] = useState<SafeAppError | null>(null);

  const load = useCallback(async (after?: string, signal?: AbortSignal) => {
    if (workspace.branch === null) return;
    setLoading(true); setError(null);
    try {
      const result = await sales.listSales(workspace.organizationId, workspace.branch.id, PAGE_SIZE, after, signal);
      setPage((current) => after === undefined || current === null
        ? firstPageState(result.items, result.nextCursor)
        : appendPage(current, result.items, result.nextCursor));
    } catch (caught) {
      const safe = mapSafeError(caught);
      if (safe.code !== "cancelled") setError(safe);
    } finally {
      setLoading(false);
    }
  }, [sales, workspace.branch, workspace.organizationId]);

  useEffect(() => {
    setPage(null); setSelected(null);
    const controller = new AbortController();
    void load(undefined, controller.signal);
    return () => controller.abort();
  }, [load]);

  async function openSale(summary: SaleSummary) {
    if (workspace.branch === null) return;
    if (selected?.id === summary.id) { setSelected(null); return; }
    setDetailLoading(true); setError(null);
    try {
      setSelected(await sales.readSale(workspace.organizationId, workspace.branch.id, summary.id));
    } catch (caught) {
      const safe = mapSafeError(caught);
      if (safe.code !== "cancelled") setError(safe);
    } finally {
      setDetailLoading(false);
    }
  }

  return <Screen>
    <ScreenHeader title={t("sales.title")} onBack={() => router.back()} />
    <Text style={textStyles.body}>{t("sales.explanation")}</Text>
    {workspace.branch === null ? <>
      <EmptyState message={t("workspace.branchRequired")} />
      <AppButton onPress={() => router.push("/stores")}>{t("workspace.chooseBranch")}</AppButton>
    </> : <>
      <Text style={operationStyles.muted}>{t("workspace.branch", { name: workspace.branch.name })}</Text>
      {error === null ? null : <SafeErrorNotice error={error} onRetry={() => void load()} />}
      {page === null && loading ? <LoadingSurface /> : null}
      {page?.items.length === 0 ? <EmptyState message={t("sales.empty")} /> : null}
      {page?.items.map((sale) => <Pressable
        key={sale.id}
        accessibilityRole="button"
        accessibilityLabel={t("sales.openDetail", { id: shortId(sale.id) })}
        onPress={() => void openSale(sale)}
        style={operationStyles.card}
      >
        <View style={operationStyles.row}>
          <Text style={operationStyles.strong}>{formatMoney(sale.grandTotal, sale.currency)}</Text>
          <Text style={operationStyles.muted}>{new Date(sale.completedAt).toLocaleString()}</Text>
        </View>
        <Text style={operationStyles.muted}>{t("sales.id", { id: shortId(sale.id) })}</Text>
        <Text style={operationStyles.muted}>{t("sales.cashSummary", {
          cash: formatMoney(sale.cashReceived, sale.currency),
          change: formatMoney(sale.changeDue, sale.currency),
        })}</Text>
        {selected?.id !== sale.id ? null : <SaleDetail sale={selected} />}
      </Pressable>)}
      {detailLoading ? <LoadingSurface /> : null}
      {page?.nextCursor === null || page === null ? null
        : <AppButton disabled={loading} onPress={() => void load(page.nextCursor ?? undefined)}>
          {t("common.loadMore")}
        </AppButton>}
    </>}
  </Screen>;
}

function SaleDetail({ sale }: Readonly<{ sale: CompletedSale }>) {
  const { t } = useLocalization();
  return <View style={{ gap: 8 }}>
    <Text style={textStyles.heading}>{t("sales.detail")}</Text>
    <Text style={operationStyles.muted}>{t("sales.financials", {
      net: formatMoney(sale.netTotal, sale.currency),
      tax: formatMoney(sale.taxTotal, sale.currency),
      total: formatMoney(sale.grandTotal, sale.currency),
    })}</Text>
    <Text style={operationStyles.muted}>{t("sales.shift", { id: sale.shiftId === null ? "—" : shortId(sale.shiftId) })}</Text>
    <Text style={operationStyles.muted}>{t("sales.register", { id: sale.registerId === null ? "—" : shortId(sale.registerId) })}</Text>
    <Text style={operationStyles.strong}>{t("sales.lines", { count: String(sale.lines.length) })}</Text>
    {sale.lines.map((line) => <View key={line.lineNumber} style={{ gap: 2 }}>
      <Text style={operationStyles.strong}>{t("sales.line", {
        number: String(line.lineNumber),
        total: formatMoney(line.grossAmount, line.currency),
      })}</Text>
      <Text style={operationStyles.muted}>{t("sales.product", { id: shortId(line.productId) })}</Text>
      <Text style={operationStyles.muted}>{t("sales.lineMath", {
        quantity: formatQuantity(line.quantity),
        unit: formatMoney(line.unitAmount, line.currency),
        tax: formatMoney(line.taxAmount, line.currency),
      })}</Text>
    </View>)}
  </View>;
}

export function formatMoney(value: number, currency: string): string {
  return `${value.toFixed(2)} ${currency}`;
}

export function formatQuantity(value: number): string {
  return value.toLocaleString(undefined, { maximumFractionDigits: 6, useGrouping: false });
}

function shortId(value: string): string {
  return value.slice(0, 8).toUpperCase();
}
