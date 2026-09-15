import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Pressable, Text, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { ClosedShiftSummary, PaymentEventSummary, RegisterSummary, ShiftSummary } from "@/api/operationsContracts";
import { managerStyles } from "@/components/managerStyles";
import { LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { createManagerOperations } from "@/services/managerOperations";
import { mapSafeError, safeErrorTranslationKey } from "@/services/safeError";
import { useWorkspace } from "@/state/workspace";

export function ReconciliationScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const client = useApiClient();
  const manager = useMemo(() => createManagerOperations(client), [client]);
  const { workspace } = useWorkspace();
  const [registers, setRegisters] = useState<readonly RegisterSummary[]>([]);
  const [openShifts, setOpenShifts] = useState<readonly ShiftSummary[]>([]);
  const [closedShifts, setClosedShifts] = useState<readonly ClosedShiftSummary[]>([]);
  const [payments, setPayments] = useState<readonly PaymentEventSummary[]>([]);
  const [movementCounts, setMovementCounts] = useState<Record<string, number>>({});
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");

  const load = useCallback(async (signal?: AbortSignal) => {
    if (!workspace.branch) { setRegisters([]); setOpenShifts([]); setClosedShifts([]); setPayments([]); return; }
    setLoading(true); setMessage("");
    try {
      const [registerPage, closedPage, paymentPage] = await Promise.all([
        manager.listRegisters(workspace.organizationId, workspace.branch.id, 100, undefined, signal),
        manager.listClosedShifts(workspace.organizationId, workspace.branch.id, 25, undefined, signal),
        manager.listPaymentEvents(workspace.organizationId, workspace.branch.id, 50, undefined, signal),
      ]);
      setRegisters(registerPage.items); setClosedShifts(closedPage.items); setPayments(paymentPage.items);
      const open = (await Promise.all(registerPage.items.map(register =>
        manager.readOpenShift(workspace.organizationId, workspace.branch!.id, register.id, signal)))).filter(Boolean) as ShiftSummary[];
      setOpenShifts(open);
    } catch (error) { const safe = mapSafeError(error); if (safe.code !== "cancelled") setMessage(t(safeErrorTranslationKey(safe))); }
    finally { setLoading(false); }
  }, [manager, workspace.organizationId, workspace.branch]);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  async function loadMovements(shift: ShiftSummary) {
    if (!workspace.branch) return;
    try {
      const items = await manager.listCashMovements(workspace.organizationId, workspace.branch.id, shift.id);
      setMovementCounts(current => ({ ...current, [shift.id]: items.length }));
    } catch (error) { setMessage(t(safeErrorTranslationKey(mapSafeError(error)))); }
  }

  const paymentTotal = payments.reduce((sum, event) => sum + event.amount, 0);

  return <Screen>
    <ScreenHeader title={t("reconciliation.title")} onBack={() => router.back()} />
    <Text style={textStyles.body}>{t("reconciliation.explanation")}</Text>
    <View style={managerStyles.warning}><Text style={managerStyles.strong}>Trusted terminal boundary</Text><Text style={managerStyles.muted}>{t("reconciliation.trustedBoundary")}</Text></View>
    {!workspace.branch ? <View style={managerStyles.warning}><Text style={managerStyles.muted}>Choose a store to load reconciliation data.</Text></View> : null}
    {message ? <View style={managerStyles.card}><Text style={managerStyles.danger}>{message}</Text></View> : null}
    {workspace.branch && loading ? <LoadingSurface /> : null}
    {workspace.branch && !loading ? <>
      <View style={managerStyles.row}>
        <View style={managerStyles.card}><Text style={managerStyles.label}>Registers</Text><Text style={managerStyles.strong}>{registers.length}</Text></View>
        <View style={managerStyles.card}><Text style={managerStyles.label}>Open shifts</Text><Text style={managerStyles.strong}>{openShifts.length}</Text></View>
        <View style={managerStyles.card}><Text style={managerStyles.label}>Recent payments</Text><Text style={managerStyles.strong}>{paymentTotal.toFixed(2)}</Text></View>
      </View>
      <View style={managerStyles.section}>
        <Text style={textStyles.heading}>Open shifts</Text>
        {openShifts.length === 0 ? <EmptyState message="No open shifts were returned." /> : null}
        {openShifts.map(shift => <Pressable key={shift.id} style={managerStyles.card} onPress={() => void loadMovements(shift)}>
          <Text style={managerStyles.strong}>Register {shift.registerId.slice(0, 8).toUpperCase()}</Text>
          <Text style={managerStyles.muted}>{shift.openingBalance.toFixed(2)} {shift.currency} · {new Date(shift.openedAt).toLocaleString()}</Text>
          <Text style={managerStyles.mono}>{shift.id}</Text>
          <Text style={managerStyles.muted}>{movementCounts[shift.id] === undefined ? "Tap to load cash movement count" : `${movementCounts[shift.id]} cash movements`}</Text>
        </Pressable>)}
      </View>
      <View style={managerStyles.section}>
        <Text style={textStyles.heading}>Closed shifts</Text>
        {closedShifts.map(shift => <View key={shift.id} style={managerStyles.card}>
          <Text style={managerStyles.strong}>Variance {shift.variance.toFixed(2)} {shift.currency}</Text>
          <Text style={managerStyles.muted}>Expected {shift.expectedCash.toFixed(2)} · Counted {shift.countedCash.toFixed(2)}</Text>
          <Text style={managerStyles.muted}>{new Date(shift.closedAt).toLocaleString()}</Text>
        </View>)}
      </View>
      <View style={managerStyles.section}>
        <Text style={textStyles.heading}>Payment events</Text>
        {payments.length === 0 ? <EmptyState message="No recent payment events were returned." /> : null}
        {payments.map(event => <View key={event.id} style={managerStyles.card}>
          <Text style={managerStyles.strong}>{event.kind.replace("_", " ")} · {event.amount.toFixed(2)} {event.currency}</Text>
          <Text style={managerStyles.muted}>{event.method} · {event.status} · {new Date(event.completedAt).toLocaleString()}</Text>
          <Text style={managerStyles.mono}>{event.sourceId}</Text>
        </View>)}
      </View>
    </> : null}
  </Screen>;
}
