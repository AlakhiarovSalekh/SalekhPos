import { useRouter } from "expo-router";
import { useMemo, useRef, useState } from "react";
import { Text, View } from "react-native";

import type { InventoryMovementKind } from "@/api/contracts";
import { useApiClient } from "@/api/ApiContext";
import { AppButton, Screen } from "@/components/primitives";
import { EmptyState, Field, SafeErrorNotice, ScreenHeader, operationStyles } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { mobileReadCache } from "@/offline/cache";
import { createInventoryMovementIntent, createMobileOperations, type InventoryMovementIntent } from "@/services/mobileOperations";
import { mapSafeError, type SafeAppError } from "@/services/safeError";
import { useWorkspace } from "@/state/workspace";

export function InventoryMovementScreen({ mode }: Readonly<{ mode: "receipt" | "adjustment" }>) {
  const router = useRouter();
  const { t } = useLocalization();
  const { workspace } = useWorkspace();
  const client = useApiClient();
  const operations = useMemo(() => createMobileOperations(client, mobileReadCache), [client]);
  const [productId, setProductId] = useState("");
  const [quantity, setQuantity] = useState("");
  const [reason, setReason] = useState("");
  const [direction, setDirection] = useState<"adjustment_in" | "adjustment_out">("adjustment_in");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<SafeAppError | null>(null);
  const [validationError, setValidationError] = useState(false);
  const [completed, setCompleted] = useState(false);
  const pending = useRef<{ fingerprint: string; intent: InventoryMovementIntent } | null>(null);
  const kind: InventoryMovementKind = mode === "receipt" ? "receipt" : direction;

  const submit = async () => {
    if (workspace.branch === null || submitting) return;
    setError(null); setValidationError(false); setCompleted(false);
    const fingerprint = JSON.stringify([workspace.organizationId, workspace.branch.id, productId, kind, quantity, reason]);
    let intent: InventoryMovementIntent;
    try {
      intent = pending.current?.fingerprint === fingerprint
        ? pending.current.intent
        : createInventoryMovementIntent({ organizationId: workspace.organizationId, branchId: workspace.branch.id, productId, kind, quantity, reason });
      pending.current = { fingerprint, intent };
    } catch { setValidationError(true); return; }
    setSubmitting(true);
    try {
      await operations.recordInventoryMovement(intent);
      pending.current = null; setCompleted(true); setQuantity(""); setReason("");
    } catch (caught) { setError(mapSafeError(caught)); }
    finally { setSubmitting(false); }
  };

  return <Screen>
    <ScreenHeader title={mode === "receipt" ? t("movement.receiptTitle") : t("movement.adjustmentTitle")} onBack={() => router.back()} />
    {workspace.branch === null ? <><EmptyState message={t("workspace.branchRequired")} /><AppButton onPress={() => router.replace("/stores")}>{t("workspace.chooseBranch")}</AppButton></> : <>
      <Text style={operationStyles.muted}>{t("workspace.branch", { name: workspace.branch.name })}</Text>
      {mode === "adjustment" ? <View style={operationStyles.row}>
        <AppButton disabled={direction === "adjustment_in"} onPress={() => { setDirection("adjustment_in"); pending.current = null; }}>{t("movement.increase")}</AppButton>
        <AppButton disabled={direction === "adjustment_out"} onPress={() => { setDirection("adjustment_out"); pending.current = null; }}>{t("movement.decrease")}</AppButton>
      </View> : null}
      <Field label={t("movement.productId")} value={productId} onChangeText={(value) => { setProductId(value); pending.current = null; }} autoCapitalize="none" autoCorrect={false} />
      <Field label={t("movement.quantity")} value={quantity} onChangeText={(value) => { setQuantity(value); pending.current = null; }} keyboardType="decimal-pad" />
      <Field label={t("movement.reason")} value={reason} onChangeText={(value) => { setReason(value); pending.current = null; }} maxLength={200} />
      <Text style={operationStyles.muted}>{t("movement.onlineOnly")}</Text>
      {validationError ? <EmptyState message={t("movement.invalid")} /> : null}
      {error === null ? null : <SafeErrorNotice error={error} onRetry={() => void submit()} />}
      {completed ? <Text accessibilityRole="alert" style={operationStyles.strong}>{t("movement.completed")}</Text> : null}
      <AppButton disabled={submitting} onPress={() => void submit()}>{submitting ? t("common.saving") : t("common.save")}</AppButton>
    </>}
  </Screen>;
}
