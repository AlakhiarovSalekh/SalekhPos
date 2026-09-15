import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { RegisterSummary } from "@/api/operationsContracts";
import { managerStyles } from "@/components/managerStyles";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { canManageRegisters } from "@/permissions/policy";
import { createManagerOperations } from "@/services/managerOperations";
import { mapSafeError, safeErrorTranslationKey } from "@/services/safeError";
import { useSession } from "@/state/SessionContext";
import { useWorkspace } from "@/state/workspace";

export function RegistersScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const client = useApiClient();
  const manager = useMemo(() => createManagerOperations(client), [client]);
  const { session } = useSession();
  const { workspace } = useWorkspace();
  const [items, setItems] = useState<readonly RegisterSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [message, setMessage] = useState("");

  const load = useCallback(async (signal?: AbortSignal) => {
    if (!workspace.branch) { setItems([]); return; }
    setLoading(true); setMessage("");
    try { setItems((await manager.listRegisters(workspace.organizationId, workspace.branch.id, 100, undefined, signal)).items); }
    catch (error) { const safe = mapSafeError(error); if (safe.code !== "cancelled") setMessage(t(safeErrorTranslationKey(safe))); }
    finally { setLoading(false); }
  }, [manager, workspace.organizationId, workspace.branch]);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  async function create() {
    if (!workspace.branch || session === null || !canManageRegisters(session.authorization)) return;
    setSaving(true); setMessage("");
    try {
      const result = await manager.createRegister(workspace.organizationId, workspace.branch.id, { code, name });
      setItems(current => [...current.filter(item => item.id !== result.id), result]
        .sort((left, right) => left.id.localeCompare(right.id)));
      setCode(""); setName(""); setMessage("Register created successfully.");
    } catch (error) { setMessage(t(safeErrorTranslationKey(mapSafeError(error)))); }
    finally { setSaving(false); }
  }

  return <Screen>
    <ScreenHeader title={t("registers.title")} onBack={() => router.back()} />
    <Text style={textStyles.body}>{t("registers.explanation")}</Text>
    {!workspace.branch ? <View style={managerStyles.warning}><Text style={managerStyles.strong}>Store required</Text><Text style={managerStyles.muted}>Choose a store before reviewing or creating registers.</Text></View> : null}
    {message ? <View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View> : null}
    {workspace.branch && session !== null && canManageRegisters(session.authorization) ? <View style={managerStyles.card}>
      <Text style={textStyles.heading}>Provision register</Text>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Code</Text><TextInput value={code} onChangeText={value => setCode(value.toUpperCase())} autoCapitalize="characters" maxLength={32} style={managerStyles.input} /></View>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Name</Text><TextInput value={name} onChangeText={setName} maxLength={100} style={managerStyles.input} /></View>
      <AppButton disabled={saving || !code.trim() || !name.trim()} onPress={() => void create()}>{saving ? "Creating…" : "Create register"}</AppButton>
    </View> : null}
    {workspace.branch && loading ? <LoadingSurface /> : null}
    {workspace.branch && !loading && items.length === 0 ? <EmptyState message="No registers were returned for this store." /> : null}
    {items.map(item => <View key={item.id} style={managerStyles.card}>
      <Text style={managerStyles.strong}>{item.name}</Text>
      <Text style={managerStyles.mono}>{item.code} · {item.id.slice(0, 8).toUpperCase()}</Text>
      <Text style={managerStyles.muted}>{item.isActive ? "Active" : "Inactive"} · Created {new Date(item.createdAt).toLocaleString()}</Text>
    </View>)}
  </Screen>;
}
