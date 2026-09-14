import { StyleSheet, Text, TextInput, View, type TextInputProps } from "react-native";

import { AppButton, colors, textStyles } from "@/components/primitives";
import { useLocalization } from "@/localization/LocalizationProvider";
import type { TranslationKey } from "@/localization/resources";
import type { SafeAppError } from "@/services/safeError";

export function ScreenHeader({ title, onBack }: Readonly<{ title: string; onBack: () => void }>) {
  const { t } = useLocalization();
  return <View style={styles.header}><AppButton onPress={onBack}>{t("common.back")}</AppButton><Text style={textStyles.title} accessibilityRole="header">{title}</Text></View>;
}

export function Field({ label, ...props }: Readonly<TextInputProps & { label: string }>) {
  return <View style={styles.field}><Text style={styles.label}>{label}</Text><TextInput {...props} accessibilityLabel={label} style={styles.input} placeholderTextColor={colors.muted} /></View>;
}

export function EmptyState({ message }: Readonly<{ message: string }>) {
  return <View style={styles.notice}><Text style={textStyles.body}>{message}</Text></View>;
}

export function CacheNotice({ storedAt }: Readonly<{ storedAt: number }>) {
  const { t } = useLocalization();
  return <View style={styles.warning} accessibilityRole="alert"><Text style={styles.warningText}>{t("cache.stale", { time: new Date(storedAt).toLocaleString() })}</Text></View>;
}

export function SafeErrorNotice({ error, onRetry }: Readonly<{ error: SafeAppError; onRetry?: () => void }>) {
  const { t } = useLocalization();
  return <View style={styles.notice} accessibilityRole="alert"><Text style={styles.error}>{t(errorKeys[error.code])}</Text>{onRetry === undefined ? null : <AppButton onPress={onRetry}>{t("common.retry")}</AppButton>}</View>;
}

const errorKeys: Readonly<Record<SafeAppError["code"], TranslationKey>> = {
  cancelled: "apiError.cancelled",
  offline: "apiError.offline",
  unauthenticated: "apiError.unauthenticated",
  forbidden: "apiError.forbidden",
  not_found: "apiError.not_found",
  conflict: "apiError.conflict",
  invalid: "apiError.invalid",
  unsafe_response: "apiError.unsafe_response",
  unavailable: "apiError.unavailable",
};

export const operationStyles = StyleSheet.create({
  card: { padding: 16, gap: 6, borderRadius: 14, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.surface },
  row: { flexDirection: "row", flexWrap: "wrap", gap: 10, alignItems: "center" },
  strong: { color: colors.text, fontSize: 17, fontWeight: "700" },
  muted: { color: colors.muted, fontSize: 14, lineHeight: 20 },
});

const styles = StyleSheet.create({
  header: { gap: 16 },
  field: { gap: 6 },
  label: { color: colors.text, fontSize: 15, fontWeight: "700" },
  input: { minHeight: 48, borderWidth: 1, borderColor: colors.border, borderRadius: 12, paddingHorizontal: 14, color: colors.text, backgroundColor: colors.surface, fontSize: 16 },
  notice: { padding: 16, gap: 12, borderRadius: 12, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.surface },
  warning: { padding: 12, borderRadius: 10, backgroundColor: "#FFF4CE" },
  warningText: { color: "#6B4F00", fontSize: 14 },
  error: { color: colors.danger, fontSize: 16, lineHeight: 22 },
});
