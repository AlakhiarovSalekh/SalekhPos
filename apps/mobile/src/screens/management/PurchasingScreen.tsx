import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { PurchaseOrderSummary, SupplierSummary } from "@/api/managementContracts";
import { managerStyles } from "@/components/managerStyles";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { hasPermission } from "@/permissions/policy";
import { createManagerBusiness } from "@/services/managerBusiness";
import { mapSafeError, safeErrorTranslationKey } from "@/services/safeError";
import { useSession } from "@/state/SessionContext";
import { useWorkspace } from "@/state/workspace";

export function PurchasingScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const client = useApiClient();
  const manager = useMemo(() => createManagerBusiness(client), [client]);
  const { session } = useSession();
  const { workspace } = useWorkspace();
  const [orders, setOrders] = useState<readonly PurchaseOrderSummary[]>([]);
  const [suppliers, setSuppliers] = useState<readonly SupplierSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [supplierId, setSupplierId] = useState("");
  const [currency, setCurrency] = useState("USD");
  const [reference, setReference] = useState("");
  const [productId, setProductId] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [unitCost, setUnitCost] = useState("");

  const load = useCallback(async (signal?: AbortSignal) => {
    if (!workspace.branch) { setOrders([]); return; }
    setLoading(true); setMessage("");
    try {
      const [orderPage, supplierPage] = await Promise.all([
        manager.listPurchaseOrders(workspace.organizationId, workspace.branch.id, 100, signal),
        manager.listSuppliers(workspace.organizationId, 100, signal),
      ]);
      setOrders(orderPage.items);
      setSuppliers(supplierPage.items.filter(item => item.isActive));
      setSupplierId(current => supplierPage.items.some(item => item.id === current)
        ? current : (supplierPage.items.find(item => item.isActive)?.id ?? ""));
    } catch (error) {
      const safe = mapSafeError(error);
      if (safe.code !== "cancelled") setMessage(t(safeErrorTranslationKey(safe)));
    } finally { setLoading(false); }
  }, [manager, t, workspace.branch, workspace.organizationId]);
  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  async function createOrder() {
    if (!workspace.branch || session === null || !hasPermission(session.authorization, "purchase_orders.create")) return;
    setSaving(true); setMessage("");
    try {
      const order = await manager.createPurchaseOrder(workspace.organizationId, workspace.branch.id, {
        supplierId,
        currency: currency.trim().toUpperCase(),
        reference,
        lines: [{ productId: productId.trim(), quantity: Number(quantity), unitCost: Number(unitCost) }],
      });
      setOrders(current => [order, ...current.filter(item => item.id !== order.id)]);
      setProductId(""); setQuantity("1"); setUnitCost(""); setReference("");
      setMessage("Purchase order created.");
    } catch (error) { setMessage(t(safeErrorTranslationKey(mapSafeError(error)))); }
    finally { setSaving(false); }
  }

  async function changeStatus(order: PurchaseOrderSummary, action: "submit" | "approve" | "cancel") {
    if (!workspace.branch) return;
    setSaving(true); setMessage("");
    try {
      const changed = await manager.changePurchaseOrderStatus(workspace.organizationId, workspace.branch.id, order, action);
      setOrders(current => current.map(item => item.id === changed.id ? changed : item));
    } catch (error) { setMessage(t(safeErrorTranslationKey(mapSafeError(error)))); }
    finally { setSaving(false); }
  }
  const canCreate = session !== null && hasPermission(session.authorization, "purchase_orders.create");
  return <Screen>
    <ScreenHeader title={t("management.purchasing")} onBack={() => router.back()} />
    <Text style={textStyles.body}>Create and review branch purchase orders with controlled lifecycle transitions.</Text>
    {!workspace.branch ? <View style={managerStyles.warning}><Text style={managerStyles.strong}>Store required</Text><Text style={managerStyles.muted}>Choose a store before managing purchase orders.</Text></View> : null}
    {message ? <View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View> : null}
    {workspace.branch && canCreate ? <View style={managerStyles.card}>
      <Text style={textStyles.heading}>New purchase order</Text>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Supplier UUID</Text><TextInput value={supplierId} onChangeText={setSupplierId} style={managerStyles.input} /></View>
      <Text style={managerStyles.muted}>{suppliers.find(item => item.id === supplierId)?.name ?? "Choose an active supplier ID."}</Text>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Product UUID</Text><TextInput value={productId} onChangeText={setProductId} style={managerStyles.input} /></View>
      <View style={managerStyles.row}>
        <View style={[managerStyles.field,{flex:1}]}><Text style={managerStyles.label}>Quantity</Text><TextInput value={quantity} onChangeText={setQuantity} keyboardType="decimal-pad" style={managerStyles.input} /></View>
        <View style={[managerStyles.field,{flex:1}]}><Text style={managerStyles.label}>Unit cost</Text><TextInput value={unitCost} onChangeText={setUnitCost} keyboardType="decimal-pad" style={managerStyles.input} /></View>
      </View>
      <View style={managerStyles.row}>
        <View style={[managerStyles.field,{flex:1}]}><Text style={managerStyles.label}>Currency</Text><TextInput value={currency} onChangeText={value=>setCurrency(value.toUpperCase())} maxLength={3} style={managerStyles.input} /></View>
        <View style={[managerStyles.field,{flex:2}]}><Text style={managerStyles.label}>Reference</Text><TextInput value={reference} onChangeText={setReference} maxLength={120} style={managerStyles.input} /></View>
      </View>
      <AppButton disabled={saving || !supplierId || !productId || !unitCost} onPress={() => void createOrder()}>{saving ? "Saving…" : "Create draft"}</AppButton>
    </View> : null}
    {workspace.branch && loading ? <LoadingSurface /> : null}
    {workspace.branch && !loading && orders.length === 0 ? <EmptyState message="No purchase orders were returned." /> : null}
    {orders.map(order => <View key={order.id} style={managerStyles.card}>
      <Text style={managerStyles.strong}>{order.reference ?? `Order ${order.id.slice(0,8).toUpperCase()}`}</Text>
      <Text style={managerStyles.mono}>{order.status.toUpperCase()} · {order.currency} {order.total.toFixed(2)} · v{order.version}</Text>
      <Text style={managerStyles.muted}>Supplier {order.supplierId.slice(0,8).toUpperCase()} · {order.lines.length} line(s)</Text>
      <View style={managerStyles.row}>
        {order.status === "draft" && session && hasPermission(session.authorization,"purchase_orders.submit") ? <AppButton disabled={saving} onPress={() => void changeStatus(order,"submit")}>Submit</AppButton> : null}
        {order.status === "submitted" && session && hasPermission(session.authorization,"purchase_orders.approve") ? <AppButton disabled={saving} onPress={() => void changeStatus(order,"approve")}>Approve</AppButton> : null}
        {order.status !== "approved" && order.status !== "cancelled" && session && hasPermission(session.authorization,"purchase_orders.cancel") ? <AppButton disabled={saving} onPress={() => void changeStatus(order,"cancel")}>Cancel</AppButton> : null}
      </View>
    </View>)}
  </Screen>;
}
