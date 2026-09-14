import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
  type PressableProps,
} from "react-native";
import type { PropsWithChildren } from "react";
import { SafeAreaView } from "react-native-safe-area-context";

import { useLocalization } from "@/localization/LocalizationProvider";

export const colors = Object.freeze({
  background: "#F4F7FB",
  surface: "#FFFFFF",
  text: "#10213A",
  muted: "#53657D",
  primary: "#075E54",
  primaryPressed: "#06483F",
  onPrimary: "#FFFFFF",
  border: "#C7D2E0",
  danger: "#A6192E",
});

export function Screen({ children }: PropsWithChildren) {
  return (
    <SafeAreaView style={styles.safeArea} edges={["top", "right", "bottom", "left"]}>
      <ScrollView
        contentContainerStyle={styles.screen}
        keyboardShouldPersistTaps="handled"
      >
        {children}
      </ScrollView>
    </SafeAreaView>
  );
}

export function AppButton({
  children,
  disabled,
  style,
  ...props
}: PropsWithChildren<PressableProps>) {
  return (
    <Pressable
      accessibilityRole="button"
      disabled={disabled}
      style={(state) => [
        styles.button,
        state.pressed && styles.buttonPressed,
        disabled && styles.buttonDisabled,
        typeof style === "function" ? style(state) : style,
      ]}
      {...props}
    >
      <Text style={styles.buttonText}>{children}</Text>
    </Pressable>
  );
}

export function LoadingSurface() {
  const { t } = useLocalization();
  return (
    <View style={styles.centered} accessibilityRole="progressbar" accessibilityLabel={t("common.loading")}>
      <ActivityIndicator size="large" color={colors.primary} />
      <Text style={styles.message}>{t("common.loading")}</Text>
    </View>
  );
}

export function ErrorSurface({
  title,
  message,
  onRetry,
}: Readonly<{ title: string; message: string; onRetry?: () => void }>) {
  const { t } = useLocalization();
  return (
    <View style={styles.centered} accessibilityRole="alert">
      <Text style={styles.errorTitle}>{title}</Text>
      <Text style={styles.message}>{message}</Text>
      {onRetry === undefined ? null : (
        <AppButton onPress={onRetry} accessibilityLabel={t("common.retry")}>
          {t("common.retry")}
        </AppButton>
      )}
    </View>
  );
}

export const textStyles = StyleSheet.create({
  title: {
    color: colors.text,
    fontSize: 30,
    fontWeight: "700",
    lineHeight: 38,
  },
  heading: {
    color: colors.text,
    fontSize: 20,
    fontWeight: "700",
    lineHeight: 28,
  },
  body: {
    color: colors.muted,
    fontSize: 16,
    lineHeight: 24,
  },
});

const styles = StyleSheet.create({
  safeArea: { flex: 1, backgroundColor: colors.background },
  screen: { flexGrow: 1, padding: 24, gap: 20 },
  centered: {
    flex: 1,
    minHeight: 320,
    padding: 24,
    alignItems: "center",
    justifyContent: "center",
    gap: 16,
    backgroundColor: colors.background,
  },
  button: {
    minHeight: 48,
    minWidth: 48,
    paddingHorizontal: 20,
    paddingVertical: 12,
    borderRadius: 12,
    backgroundColor: colors.primary,
    alignItems: "center",
    justifyContent: "center",
  },
  buttonPressed: { backgroundColor: colors.primaryPressed },
  buttonDisabled: { opacity: 0.55 },
  buttonText: { color: colors.onPrimary, fontSize: 16, fontWeight: "700" },
  errorTitle: { color: colors.danger, fontSize: 22, fontWeight: "700", textAlign: "center" },
  message: { color: colors.muted, fontSize: 16, lineHeight: 24, textAlign: "center" },
});
