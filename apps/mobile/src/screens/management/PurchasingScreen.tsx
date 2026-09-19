import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { PurchaseOrderSummary, PurchaseReceiptSummary, PurchaseReceivingStateSummary, SupplierSummary } from "@/api/managementContracts";
import { managerStyles } from "@/components/managerStyles";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { hasPermission, permissions } from "@/permissions/policy";
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
  const [receivingOrder, setReceivingOrder] = useState<PurchaseOrderSummary | null>(null);
  const [receivingState, setReceivingState] = useState<PurchaseReceivingStateSummary | null>(null);
  const [receiptQuantities, setReceiptQuantities] = useState<Record<string, string>>({});
  const [receiptReference, setReceiptReference] = useState("");
  const [historyOrder, setHistoryOrder] = useState<PurchaseOrderSummary | null>(null);
  const [receiptHistory, setReceiptHistory] = useState<readonly PurchaseReceiptSummary[]>([]);
  const [historyNextCursor, setHistoryNextCursor] = useState<string | null>(null);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [loadedScopeKey, setLoadedScopeKey] = useState("");
  const scopeGeneration = useRef(0);

  const currentScopeKey = workspace.branch
    ? `${workspace.organizationId}:${workspace.branch.id}`
    : "";
  const scopeReady = currentScopeKey !== "" && loadedScopeKey === currentScopeKey;
  const scopedOrders = scopeReady ? orders : [];
  const scopedSuppliers = scopeReady ? suppliers : [];

  const load = useCallback(async (signal?: AbortSignal) => {
    const generation = scopeGeneration.current;
    if (!workspace.branch) {
      if (generation === scopeGeneration.current) {
        setOrders([]);
        setLoadedScopeKey("");
      }
      return;
    }
    setLoading(true); setMessage("");
    try {
      const [orderPage, supplierPage] = await Promise.all([
        manager.listPurchaseOrders(workspace.organizationId, workspace.branch.id, 100, signal),
        manager.listSuppliers(workspace.organizationId, 100, signal),
      ]);
      if (generation !== scopeGeneration.current) return;
      setOrders(orderPage.items);
      setSuppliers(supplierPage.items.filter(item => item.isActive));
      setLoadedScopeKey(`${workspace.organizationId}:${workspace.branch.id}`);
      setSupplierId(current => supplierPage.items.some(item => item.id === current)
        ? current : (supplierPage.items.find(item => item.isActive)?.id ?? ""));
    } catch (error) {
      if (generation !== scopeGeneration.current) return;
      const safe = mapSafeError(error);
      if (safe.code !== "cancelled") setMessage(t(safeErrorTranslationKey(safe)));
    } finally {
      if (generation === scopeGeneration.current) setLoading(false);
    }
  }, [manager, t, workspace.branch, workspace.organizationId]);
  useEffect(() => {
    scopeGeneration.current += 1;
    setReceivingOrder(null);
    setReceivingState(null);
    setReceiptQuantities({});
    setReceiptReference("");
    setHistoryOrder(null);
    setReceiptHistory([]);
    setHistoryNextCursor(null);
    const controller = new AbortController();
    void load(controller.signal);
    return () => { scopeGeneration.current += 1; controller.abort(); };
  }, [load]);

  async function createOrder() {
    if (!scopeReady || !workspace.branch || session === null || !hasPermission(session.authorization, "purchase_orders.create")) return;
    const generation = scopeGeneration.current;
    setSaving(true); setMessage("");
    try {
      const order = await manager.createPurchaseOrder(workspace.organizationId, workspace.branch.id, {
        supplierId,
        currency: currency.trim().toUpperCase(),
        reference,
        lines: [{ productId: productId.trim(), quantity: Number(quantity), unitCost: Number(unitCost) }],
      });
      if (generation !== scopeGeneration.current) return;
      setOrders(current => [order, ...current.filter(item => item.id !== order.id)]);
      setProductId(""); setQuantity("1"); setUnitCost(""); setReference("");
      setMessage("Purchase order created.");
    } catch (error) {
      if (generation === scopeGeneration.current)
        setMessage(t(safeErrorTranslationKey(mapSafeError(error))));
    } finally {
      if (generation === scopeGeneration.current) setSaving(false);
    }
  }

  async function openReceiving(order: PurchaseOrderSummary) {
    if (!scopeReady || !workspace.branch || session === null
      || !hasPermission(session.authorization, permissions.inventoryReceive)
      || (order.status !== "approved" && order.status !== "partially_received")) return;
    const generation = scopeGeneration.current;
    setSaving(true); setMessage("");
    try {
      const state = await manager.readPurchaseReceivingState(
        workspace.organizationId,
        workspace.branch.id,
        order.id,
      );
      if (generation !== scopeGeneration.current) return;
      setReceivingState(state);
      setReceivingOrder({ ...order, status: state.status, version: state.version });
      setReceiptQuantities(Object.fromEntries(
        state.lines.filter(line => line.remainingQuantity > 0)
          .map(line => [line.productId, String(line.remainingQuantity)]),
      ));
      setReceiptReference("");
    } catch (error) {
      if (generation === scopeGeneration.current)
        setMessage(t(safeErrorTranslationKey(mapSafeError(error))));
    } finally {
      if (generation === scopeGeneration.current) setSaving(false);
    }
  }

  async function receiveGoods() {
    if (!scopeReady || !workspace.branch || !receivingOrder || !receivingState) return;
    const lines = receivingState.lines.map(line => ({
      productId: line.productId,
      quantity: Number(receiptQuantities[line.productId] ?? "0"),
    })).filter(line => Number.isFinite(line.quantity) && line.quantity > 0);
    if (lines.length === 0) {
      setMessage("Enter at least one positive receipt quantity.");
      return;
    }

    const generation = scopeGeneration.current;
    setSaving(true); setMessage("");
    try {
      const normalizedReceiptReference = receiptReference.trim();
      const result = await manager.receivePurchaseOrder(
        workspace.organizationId,
        workspace.branch.id,
        receivingOrder,
        {
          ...(normalizedReceiptReference ? { reference: normalizedReceiptReference } : {}),
          receivedAt: new Date().toISOString(),
          lines,
        },
      );
      if (generation !== scopeGeneration.current) return;
      setOrders(current => current.map(item =>
        item.id === result.order.id ? result.order : item));
      if (historyOrder?.id === result.order.id) {
        setHistoryOrder(result.order);
        setReceiptHistory(current => [
          result.receipt,
          ...current.filter(item => item.id !== result.receipt.id),
        ]);
      }
      setReceivingOrder(null);
      setReceivingState(null);
      setReceiptQuantities({});
      setReceiptReference("");
      setMessage("Goods receipt recorded and inventory updated.");
      await load();
    } catch (error) {
      if (generation === scopeGeneration.current)
        setMessage(t(safeErrorTranslationKey(mapSafeError(error))));
    } finally {
      if (generation === scopeGeneration.current) setSaving(false);
    }
  }

  async function openReceiptHistory(order: PurchaseOrderSummary) {
    if (!scopeReady || !workspace.branch) return;
    const generation = scopeGeneration.current;
    setHistoryLoading(true); setMessage("");
    setHistoryOrder(order);
    setReceiptHistory([]);
    setHistoryNextCursor(null);
    try {
      const page = await manager.listPurchaseReceipts(
        workspace.organizationId, workspace.branch.id, order.id, 25);
      if (generation !== scopeGeneration.current) return;
      setReceiptHistory(page.items);
      setHistoryNextCursor(page.nextCursor);
    } catch (error) {
      if (generation === scopeGeneration.current)
        setMessage(t(safeErrorTranslationKey(mapSafeError(error))));
    } finally {
      if (generation === scopeGeneration.current) setHistoryLoading(false);
    }
  }

  async function loadMoreReceipts() {
    if (!scopeReady || !workspace.branch || !historyOrder || !historyNextCursor) return;
    const generation = scopeGeneration.current;
    setHistoryLoading(true); setMessage("");
    try {
      const previousCursor = historyNextCursor;
      const page = await manager.listPurchaseReceipts(
        workspace.organizationId, workspace.branch.id, historyOrder.id, 25, previousCursor);
      if (generation !== scopeGeneration.current) return;
      if (page.nextCursor === previousCursor) throw new Error("Repeated receipt cursor");
      setReceiptHistory(current => {
        const seen = new Set(current.map(item => item.id));
        return [...current, ...page.items.filter(item => !seen.has(item.id))];
      });
      setHistoryNextCursor(page.nextCursor);
    } catch (error) {
      if (generation === scopeGeneration.current)
        setMessage(t(safeErrorTranslationKey(mapSafeError(error))));
    } finally {
      if (generation === scopeGeneration.current) setHistoryLoading(false);
    }
  }

  async function changeStatus(order: PurchaseOrderSummary, action: "submit" | "approve" | "cancel") {
    if (!scopeReady || !workspace.branch) return;
    const generation = scopeGeneration.current;
    setSaving(true); setMessage("");
    try {
      const changed = await manager.changePurchaseOrderStatus(
        workspace.organizationId, workspace.branch.id, order, action);
      if (generation !== scopeGeneration.current) return;
      setOrders(current => current.map(item => item.id === changed.id ? changed : item));
    } catch (error) {
      if (generation === scopeGeneration.current)
        setMessage(t(safeErrorTranslationKey(mapSafeError(error))));
    } finally {
      if (generation === scopeGeneration.current) setSaving(false);
    }
  }
  const canCreate = scopeReady && session !== null && hasPermission(session.authorization, "purchase_orders.create");
  const canReceive = scopeReady && session !== null && hasPermission(session.authorization, permissions.inventoryReceive);
  return <Screen>
    <ScreenHeader title={t("management.purchasing")} onBack={() => router.back()} />
    <Text style={textStyles.body}>Create and review branch purchase orders with controlled lifecycle transitions.</Text>
    {!workspace.branch ? <View style={managerStyles.warning}><Text style={managerStyles.strong}>Store required</Text><Text style={managerStyles.muted}>Choose a store before managing purchase orders.</Text></View> : null}
    {message ? <View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View> : null}
    {workspace.branch && canCreate ? <View style={managerStyles.card}>
      <Text style={textStyles.heading}>New purchase order</Text>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Supplier UUID</Text><TextInput value={supplierId} onChangeText={setSupplierId} style={managerStyles.input} /></View>
      <Text style={managerStyles.muted}>{scopedSuppliers.find(item => item.id === supplierId)?.name ?? "Choose an active supplier ID."}</Text>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Product UUID</Text><TextInput value={productId} onChangeText={setProductId} style={managerStyles.input} /></View>
      <View style={managerStyles.row}>
        <View style={[managerStyles.field,{flex:1}]}><Text style={managerStyles.label}>Quantity</Text><TextInput value={quantity} onChangeText={setQuantity} keyboardType="decimal-pad" style={managerStyles.input} /></View>
        <View style={[managerStyles.field,{flex:1}]}><Text style={managerStyles.label}>Unit cost</Text><TextInput value={unitCost} onChangeText={setUnitCost} keyboardType="decimal-pad" style={managerStyles.input} /></View>
      </View>
      <View style={managerStyles.row}>
        <View style={[managerStyles.field,{flex:1}]}><Text style={managerStyles.label}>Currency</Text><TextInput value={currency} onChangeText={value=>setCurrency(value.toUpperCase())} maxLength={3} style={managerStyles.input} /></View>
        <View style={[managerStyles.field,{flex:2}]}><Text style={managerStyles.label}>Reference</Text><TextInput value={reference} onChangeText={setReference} maxLength={100} style={managerStyles.input} /></View>
      </View>
      <AppButton disabled={saving || !supplierId || !productId || !unitCost} onPress={() => void createOrder()}>{saving ? "Saving…" : "Create draft"}</AppButton>
    </View> : null}
    {workspace.branch && loading ? <LoadingSurface /> : null}
    {workspace.branch && !loading && scopeReady && scopedOrders.length === 0 ? <EmptyState message="No purchase orders were returned." /> : null}
    {scopedOrders.map(order => <View key={order.id} style={managerStyles.card}>
      <Text style={managerStyles.strong}>{order.reference ?? `Order ${order.id.slice(0,8).toUpperCase()}`}</Text>
      <Text style={managerStyles.mono}>{order.status.toUpperCase()} · {order.currency} {order.total.toFixed(2)} · v{order.version}</Text>
      <Text style={managerStyles.muted}>Supplier {order.supplierId.slice(0,8).toUpperCase()} · {order.lines.length} line(s)</Text>
      <View style={managerStyles.row}>
        {order.status === "draft" && session && hasPermission(session.authorization,"purchase_orders.submit") ? <AppButton disabled={saving} onPress={() => void changeStatus(order,"submit")}>Submit</AppButton> : null}
        {order.status === "submitted" && session && hasPermission(session.authorization,"purchase_orders.approve") ? <AppButton disabled={saving} onPress={() => void changeStatus(order,"approve")}>Approve</AppButton> : null}
        {order.status !== "approved" && order.status !== "received" && order.status !== "partially_received" && order.status !== "cancelled" && session && hasPermission(session.authorization,"purchase_orders.cancel") ? <AppButton disabled={saving} onPress={() => void changeStatus(order,"cancel")}>Cancel</AppButton> : null}
        {canReceive && (order.status === "approved" || order.status === "partially_received") ? <AppButton disabled={saving} onPress={() => void openReceiving(order)}>Receive goods</AppButton> : null}
        <AppButton disabled={saving || historyLoading} onPress={() => void openReceiptHistory(order)}>Receipts</AppButton>
      </View>
    </View>)}
    {scopeReady && historyOrder ? <View style={managerStyles.card}>
      <Text style={textStyles.heading}>Receipt history</Text>
      <Text style={managerStyles.muted}>
        {historyOrder.reference ?? `Order ${historyOrder.id.slice(0,8).toUpperCase()}`} · {receiptHistory.length} receipt(s) loaded
      </Text>
      {receiptHistory.length === 0
        ? <Text style={managerStyles.muted}>No receipts have been recorded for this purchase order.</Text>
        : [...receiptHistory].sort((a,b)=>b.createdAt.localeCompare(a.createdAt)).map(receipt =>
          <View key={receipt.id} style={managerStyles.field}>
            <Text style={managerStyles.strong}>{receipt.reference ?? `Receipt ${receipt.id.slice(0,8).toUpperCase()}`}</Text>
            <Text style={managerStyles.muted}>{new Date(receipt.receivedAt).toLocaleString()} · {receipt.lines.length} line(s)</Text>
            <Text style={managerStyles.muted}>Received by {receipt.receivedBySubject}</Text>
            {receipt.lines.map(line => <Text key={line.movementId} style={managerStyles.mono}>
              {line.productId.slice(0,8).toUpperCase()} · {line.quantity}
            </Text>)}
          </View>)}
      <View style={managerStyles.row}>
        {historyNextCursor ? <AppButton disabled={historyLoading} onPress={() => void loadMoreReceipts()}>{historyLoading ? "Loading…" : "Load more"}</AppButton> : null}
        <AppButton disabled={historyLoading} onPress={() => { setHistoryOrder(null); setReceiptHistory([]); setHistoryNextCursor(null); }}>Close</AppButton>
      </View>
    </View> : null}
    {scopeReady && receivingOrder && receivingState ? <View style={managerStyles.card}>
      <Text style={textStyles.heading}>Receive purchase order</Text>
      <Text style={managerStyles.muted}>Record only goods physically received. Inventory updates in the same server transaction.</Text>
      {receivingState.lines.map(line => <View key={line.productId} style={managerStyles.field}>
        <Text style={managerStyles.label}>Product {line.productId.slice(0,8).toUpperCase()}</Text>
        <Text style={managerStyles.muted}>Ordered {line.orderedQuantity} · received {line.receivedQuantity} · remaining {line.remainingQuantity}</Text>
        <TextInput
          value={receiptQuantities[line.productId] ?? ""}
          onChangeText={value => setReceiptQuantities(current => ({ ...current, [line.productId]: value }))}
          editable={!saving && line.remainingQuantity > 0}
          keyboardType="decimal-pad"
          placeholder="Receive now"
          style={managerStyles.input}
        />
      </View>)}
      <View style={managerStyles.field}><Text style={managerStyles.label}>Receipt reference</Text>
        <TextInput value={receiptReference} onChangeText={setReceiptReference} maxLength={120} style={managerStyles.input} />
      </View>
      <View style={managerStyles.row}>
        <AppButton disabled={saving} onPress={() => void receiveGoods()}>{saving ? "Receiving…" : "Record receipt"}</AppButton>
        <AppButton disabled={saving} onPress={() => { setReceivingOrder(null); setReceivingState(null); }}>Cancel</AppButton>
      </View>
    </View> : null}
  </Screen>;
}
