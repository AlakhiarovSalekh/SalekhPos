import { useRouter } from "expo-router";
import { useEffect, useMemo, useState } from "react";
import { Pressable, Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { Product } from "@/api/contracts";
import type { ResolvedPriceSummary } from "@/api/operationsContracts";
import { managerStyles } from "@/components/managerStyles";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { mobileReadCache } from "@/offline/cache";
import { canManagePricing } from "@/permissions/policy";
import { createManagerOperations } from "@/services/managerOperations";
import { createMobileOperations } from "@/services/mobileOperations";
import { mapSafeError, safeErrorTranslationKey } from "@/services/safeError";
import { useSession } from "@/state/SessionContext";
import { useWorkspace } from "@/state/workspace";

export function PricingScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const client = useApiClient();
  const manager = useMemo(() => createManagerOperations(client), [client]);
  const operations = useMemo(() => createMobileOperations(client, mobileReadCache), [client]);
  const { session } = useSession();
  const { workspace } = useWorkspace();
  const [products, setProducts] = useState<readonly Product[]>([]);
  const [selected, setSelected] = useState<Product | null>(null);
  const [resolved, setResolved] = useState<ResolvedPriceSummary | null>(null);
  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("USD");
  const [taxRate, setTaxRate] = useState("0");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setMessage("");
    operations.listProducts(workspace.organizationId, 50, undefined, controller.signal)
      .then(result => setProducts(result.value.items))
      .catch(error => { const safe = mapSafeError(error); if (safe.code !== "cancelled") setMessage(t(safeErrorTranslationKey(safe))); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [operations, workspace.organizationId]);

  async function selectProduct(product: Product) {
    setSelected(product); setResolved(null); setMessage("");
    if (!workspace.branch) return;
    try {
      setResolved(await manager.resolvePrice(workspace.organizationId, workspace.branch.id, product.id, new Date().toISOString()));
    } catch (error) { setMessage(t(safeErrorTranslationKey(mapSafeError(error)))); }
  }

  async function save() {
    if (!selected || session === null || !canManagePricing(session.authorization)) return;
    const numericAmount = Number(amount);
    const numericTaxRate = Number(taxRate);
    if (!Number.isFinite(numericAmount) || !Number.isFinite(numericTaxRate)) { setMessage("Enter valid numeric price and tax values."); return; }
    setSaving(true); setMessage("");
    try {
      await manager.schedulePrice(workspace.organizationId, {
        productId: selected.id, branchId: workspace.branch?.id ?? null, amount: numericAmount,
        currency: currency.trim().toUpperCase(), taxMode: "inclusive", taxRate: numericTaxRate,
        validFrom: new Date().toISOString(), validUntil: null,
      });
      setMessage("The price was scheduled successfully.");
      if (workspace.branch) setResolved(await manager.resolvePrice(workspace.organizationId, workspace.branch.id, selected.id, new Date().toISOString()));
    } catch (error) { setMessage(t(safeErrorTranslationKey(mapSafeError(error)))); }
    finally { setSaving(false); }
  }

  if (loading) return <Screen><ScreenHeader title={t("pricing.title")} onBack={() => router.back()} /><LoadingSurface /></Screen>;
  return <Screen>
    <ScreenHeader title={t("pricing.title")} onBack={() => router.back()} />
    <Text style={textStyles.body}>{t("pricing.explanation")}</Text>
    {!workspace.branch ? <View style={managerStyles.warning}><Text style={managerStyles.strong}>Store selection</Text><Text style={managerStyles.muted}>Choose a store to resolve branch-specific effective prices. Organization-wide price scheduling can still be prepared by authorized managers.</Text></View> : null}
    {message ? <View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View> : null}
    <View style={managerStyles.section}>
      {products.map(product => <Pressable key={product.id} style={managerStyles.card} onPress={() => void selectProduct(product)}>
        <Text style={managerStyles.strong}>{product.name}</Text>
        <Text style={managerStyles.mono}>{product.sku} · {product.id.slice(0, 8).toUpperCase()}</Text>
        {selected?.id === product.id ? <Text style={textStyles.body}>Selected</Text> : null}
      </Pressable>)}
    </View>
    {selected ? <View style={managerStyles.card}>
      <Text style={textStyles.heading}>{selected.name}</Text>
      {resolved ? <Text style={managerStyles.strong}>Current: {resolved.amount.toFixed(2)} {resolved.currency}</Text> : <Text style={managerStyles.muted}>No effective branch price is currently resolved.</Text>}
      {session !== null && canManagePricing(session.authorization) ? <>
        <View style={managerStyles.field}><Text style={managerStyles.label}>Amount</Text><TextInput value={amount} onChangeText={setAmount} keyboardType="decimal-pad" style={managerStyles.input} /></View>
        <View style={managerStyles.field}><Text style={managerStyles.label}>Currency</Text><TextInput value={currency} onChangeText={setCurrency} autoCapitalize="characters" maxLength={3} style={managerStyles.input} /></View>
        <View style={managerStyles.field}><Text style={managerStyles.label}>Tax rate %</Text><TextInput value={taxRate} onChangeText={setTaxRate} keyboardType="decimal-pad" style={managerStyles.input} /></View>
        <AppButton disabled={saving} onPress={() => void save()}>{saving ? "Saving…" : "Schedule price"}</AppButton>
      </> : <Text style={managerStyles.muted}>Your session has read-only pricing access.</Text>}
    </View> : null}
  </Screen>;
}
