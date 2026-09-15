import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useApiClient } from "@/api/ApiContext";
import type { CustomerSummary, EmployeeSummary, SupplierSummary } from "@/api/managementContracts";
import { managerStyles } from "@/components/managerStyles";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { hasPermission } from "@/permissions/policy";
import { createManagerBusiness } from "@/services/managerBusiness";
import { mapSafeError, safeErrorTranslationKey } from "@/services/safeError";
import { useSession } from "@/state/SessionContext";
import { useWorkspace } from "@/state/workspace";

type Kind = "customers" | "suppliers" | "employees";
type Item = CustomerSummary | SupplierSummary | EmployeeSummary;
const requiredPermissions = {
  customers: { view: "customers.view", create: "customers.create" },
  suppliers: { view: "suppliers.view", create: "suppliers.create" },
  employees: { view: "employees.view", create: "employees.create" },
} as const;
function label(item: Item): string {
  return "displayName" in item ? item.displayName : item.name;
}

export function BusinessDirectoryScreen({ kind }: Readonly<{ kind: Kind }>) {
  const router = useRouter();
  const { t } = useLocalization();
  const client = useApiClient();
  const manager = useMemo(() => createManagerBusiness(client), [client]);
  const { session } = useSession();
  const { workspace } = useWorkspace();
  const [items, setItems] = useState<readonly Item[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [extra, setExtra] = useState("");
  const branchRequired = kind === "employees";
  const load = useCallback(async (signal?: AbortSignal) => {
    if (session === null || !hasPermission(session.authorization, requiredPermissions[kind].view)) return;
    if (branchRequired && !workspace.branch) { setItems([]); return; }
    setLoading(true); setMessage("");
    try {
      const page = kind === "customers"
        ? await manager.listCustomers(workspace.organizationId, 100, signal)
        : kind === "suppliers"
          ? await manager.listSuppliers(workspace.organizationId, 100, signal)
          : await manager.listEmployees(workspace.organizationId, workspace.branch!.id, 100, signal);
      setItems(page.items);
    } catch (error) {
      const safe = mapSafeError(error);
      if (safe.code !== "cancelled") setMessage(t(safeErrorTranslationKey(safe)));
    } finally { setLoading(false); }
  }, [branchRequired, kind, manager, session, t, workspace.branch, workspace.organizationId]);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);
  async function create() {
    if (session === null || !hasPermission(session.authorization, requiredPermissions[kind].create)) return;
    if (branchRequired && !workspace.branch) return;
    setSaving(true); setMessage("");
    try {
      const created = kind === "customers"
        ? await manager.createCustomer(workspace.organizationId, { code, displayName: name, email, phone })
        : kind === "suppliers"
          ? await manager.createSupplier(workspace.organizationId, { code, name, taxId: extra, email, phone })
          : await manager.createEmployee(workspace.organizationId, workspace.branch!.id,
              { code, displayName: name, email, phone, jobTitle: extra });
      setItems(current => [...current.filter(item => item.id !== created.id), created]
        .sort((left, right) => left.code.localeCompare(right.code)));
      setCode(""); setName(""); setEmail(""); setPhone(""); setExtra("");
      setMessage("Created successfully.");
    } catch (error) {
      setMessage(t(safeErrorTranslationKey(mapSafeError(error))));
    } finally { setSaving(false); }
  }

  const title = t(kind === "customers" ? "management.customers"
    : kind === "suppliers" ? "management.suppliers" : "management.employees");
  const canCreate = session !== null && hasPermission(session.authorization, requiredPermissions[kind].create);
  return <Screen>
    <ScreenHeader title={title} onBack={() => router.back()} />
    <Text style={textStyles.body}>Manage authorized {kind} in the current organization scope.</Text>
    {branchRequired && !workspace.branch ? <View style={managerStyles.warning}>
      <Text style={managerStyles.strong}>Store required</Text>
      <Text style={managerStyles.muted}>Choose a store before managing employees.</Text>
    </View> : null}
    {message ? <View style={managerStyles.card}><Text style={managerStyles.muted}>{message}</Text></View> : null}
    {canCreate && (!branchRequired || workspace.branch) ? <View style={managerStyles.card}>
      <Text style={textStyles.heading}>Create record</Text>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Code</Text>
        <TextInput value={code} onChangeText={value => setCode(value.toUpperCase())} maxLength={32} style={managerStyles.input} /></View>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Name</Text>
        <TextInput value={name} onChangeText={setName} maxLength={200} style={managerStyles.input} /></View>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Email</Text>
        <TextInput value={email} onChangeText={setEmail} maxLength={254} autoCapitalize="none" keyboardType="email-address" style={managerStyles.input} /></View>
      <View style={managerStyles.field}><Text style={managerStyles.label}>Phone</Text>
        <TextInput value={phone} onChangeText={setPhone} maxLength={32} keyboardType="phone-pad" style={managerStyles.input} /></View>
      {kind !== "customers" ? <View style={managerStyles.field}>
        <Text style={managerStyles.label}>{kind === "employees" ? "Job title" : "Tax ID"}</Text>
        <TextInput value={extra} onChangeText={setExtra} maxLength={kind === "employees" ? 120 : 64} style={managerStyles.input} />
      </View> : null}
      <AppButton disabled={saving || !code.trim() || !name.trim() || (kind === "employees" && !extra.trim())}
        onPress={() => void create()}>{saving ? "Creating…" : "Create"}</AppButton>
    </View> : null}
    {loading ? <LoadingSurface /> : null}
    {!loading && (!branchRequired || workspace.branch) && items.length === 0 ? <EmptyState message={`No ${kind} were returned.`} /> : null}
    {items.map(item => <View key={item.id} style={managerStyles.card}>
      <Text style={managerStyles.strong}>{label(item)}</Text>
      <Text style={managerStyles.mono}>{item.code} · {item.id.slice(0, 8).toUpperCase()}</Text>
      {"jobTitle" in item ? <Text style={managerStyles.muted}>{item.jobTitle}</Text> : null}
      {"taxId" in item && item.taxId ? <Text style={managerStyles.muted}>Tax ID: {item.taxId}</Text> : null}
      {item.email ? <Text style={managerStyles.muted}>{item.email}</Text> : null}
      {item.phone ? <Text style={managerStyles.muted}>{item.phone}</Text> : null}
      <Text style={managerStyles.muted}>{item.isActive ? "Active" : "Inactive"} · v{item.version}</Text>
    </View>)}
  </Screen>;
}
