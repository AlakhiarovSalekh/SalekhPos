import { useRouter } from "expo-router";
import { useMemo, useState } from "react";
import { Text, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { OperationalReportSummary } from "@/api/managementContracts";
import { managerStyles } from "@/components/managerStyles";
import { AppButton, Screen, textStyles } from "@/components/primitives";
import { ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { createManagerBusiness } from "@/services/managerBusiness";
import { mapSafeError, safeErrorTranslationKey } from "@/services/safeError";
import { useWorkspace } from "@/state/workspace";

export function ReportsScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const client = useApiClient();
  const manager = useMemo(() => createManagerBusiness(client), [client]);
  const { workspace } = useWorkspace();
  const [report, setReport] = useState<OperationalReportSummary | null>(null);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  async function load() {
    if (!workspace.branch) return;
    setLoading(true); setMessage("");
    const to = new Date();
    const from = new Date(to.getTime() - 24 * 60 * 60 * 1000);
    try {
      setReport(await manager.readOperationalReport(workspace.organizationId, workspace.branch.id,
        from.toISOString(), to.toISOString()));
    } catch (error) {
      setMessage(t(safeErrorTranslationKey(mapSafeError(error))));
    } finally { setLoading(false); }
  }

  const money = (value: number) => report?.currency ? `${report.currency} ${value.toFixed(2)}` : value.toFixed(2);
  return <Screen>
    <ScreenHeader title={t("management.reports")} onBack={() => router.back()} />
    <Text style={textStyles.body}>Operational summary for the last 24 hours in the selected store.</Text>
    {!workspace.branch ? <View style={managerStyles.warning}><Text style={managerStyles.strong}>Store required</Text><Text style={managerStyles.muted}>Choose a store before loading reports.</Text></View> : null}
    {message ? <View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View> : null}
    {workspace.branch ? <AppButton disabled={loading} onPress={() => void load()}>{loading ? "Loading…" : "Load last 24 hours"}</AppButton> : null}
    {report ? <>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Sales</Text><Text style={managerStyles.muted}>{report.salesCount} sales · {money(report.salesGross)} gross</Text></View>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Returns</Text><Text style={managerStyles.muted}>{report.returnCount} returns · {money(report.returnsTotal)}</Text></View>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Net sales</Text><Text style={managerStyles.mono}>{money(report.netSales)}</Text></View>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Purchasing</Text><Text style={managerStyles.muted}>{report.purchaseOrderCount} orders · {money(report.purchaseOrderTotal)}</Text></View>
      <View style={managerStyles.card}><Text style={managerStyles.strong}>Open shifts</Text><Text style={managerStyles.mono}>{report.openShiftCount}</Text></View>
      <Text style={managerStyles.muted}>Window: {new Date(report.from).toLocaleString()} – {new Date(report.to).toLocaleString()}</Text>
    </> : null}
  </Screen>;
}
