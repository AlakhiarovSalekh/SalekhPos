import { useRouter } from "expo-router";
import { useMemo, useState } from "react";
import { Text, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { AnalyticsOverview, SalesTrend, StoreComparison } from "@/api/analyticsContracts";
import { managerStyles } from "@/components/managerStyles";
import { AppButton, Screen, textStyles } from "@/components/primitives";
import { ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { createAnalyticsService } from "@/services/analytics";
import { mapSafeError, safeErrorTranslationKey } from "@/services/safeError";
import { useWorkspace } from "@/state/workspace";

export function AnalyticsScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const client = useApiClient();
  const analytics = useMemo(() => createAnalyticsService(client), [client]);
  const { workspace } = useWorkspace();
  const [overview, setOverview] = useState<AnalyticsOverview | null>(null);
  const [trend, setTrend] = useState<SalesTrend | null>(null);
  const [stores, setStores] = useState<StoreComparison | null>(null);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  async function load() {
    if (!workspace.branch) return;
    setLoading(true); setMessage("");
    const to = new Date();
    const from = new Date(to.getTime() - 30 * 86_400_000);
    const fromIso = from.toISOString(); const toIso = to.toISOString();
    try {
      const [nextOverview, nextTrend] = await Promise.all([
        analytics.overview(workspace.organizationId, workspace.branch.id, fromIso, toIso),
        analytics.trend(workspace.organizationId, workspace.branch.id, fromIso, toIso),
      ]);
      setOverview(nextOverview); setTrend(nextTrend);
      try { setStores(await analytics.stores(workspace.organizationId, fromIso, toIso)); }
      catch { setStores(null); }
    } catch (error) {
      setMessage(t(safeErrorTranslationKey(mapSafeError(error))));
    } finally { setLoading(false); }
  }

  const money = (value: number, currency: string | null) => currency ? `${currency} ${value.toFixed(2)}` : value.toFixed(2);
  return <Screen>
    <ScreenHeader title={t("management.analytics")} onBack={() => router.back()} />
    <Text style={textStyles.body}>30-day sales, stock health and cross-store business analytics.</Text>
    {!workspace.branch ? <View style={managerStyles.warning}><Text style={managerStyles.strong}>Store required</Text><Text style={managerStyles.muted}>Choose a store before loading analytics.</Text></View> : null}
    {message ? <View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View> : null}
    {workspace.branch ? <AppButton disabled={loading} onPress={() => void load()}>{loading ? "Loading…" : "Load analytics"}</AppButton> : null}
    {overview ? <>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Net revenue</Text><Text style={managerStyles.mono}>{money(overview.netRevenue, overview.currency)}</Text></View>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Sales</Text><Text style={managerStyles.muted}>{overview.completedSales} completed · {money(overview.grossSales, overview.currency)} gross · {money(overview.averageTicket, overview.currency)} avg ticket</Text></View>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Returns</Text><Text style={managerStyles.muted}>{overview.completedReturns} completed · {money(overview.refunds, overview.currency)} refunded</Text></View>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Stock health</Text><Text style={managerStyles.muted}>{overview.positiveStockProducts} positive · {overview.zeroStockProducts} zero · {overview.negativeStockProducts} negative</Text></View>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Distinct products sold</Text><Text style={managerStyles.mono}>{overview.distinctProductsSold}</Text></View>
    </> : null}
    {trend?.points.slice(-7).map(point => <View key={point.bucketStart} style={managerStyles.card}><Text style={managerStyles.strong}>{new Date(point.bucketStart).toLocaleDateString()}</Text><Text style={managerStyles.muted}>{point.completedSales} sales · {money(point.netRevenue, point.currency)} net</Text></View>)}
    {stores ? <><Text style={textStyles.heading}>Store comparison</Text>{stores.items.map(item => <View key={item.branchId} style={managerStyles.card}><Text style={managerStyles.strong}>{item.branchId}</Text><Text style={managerStyles.muted}>{item.completedSales} sales · {money(item.netRevenue, item.currency)} net · {money(item.averageTicket, item.currency)} avg</Text></View>)}</> : null}
  </Screen>;
}
