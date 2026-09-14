import { StyleSheet, Text, View } from "react-native";

import { Screen, colors, textStyles } from "@/components/primitives";
import { useLocalization } from "@/localization/LocalizationProvider";

export function SignInScreen() {
  const { t } = useLocalization();
  return (
    <Screen>
      <View style={styles.brand} accessible accessibilityRole="header">
        <Text style={styles.brandText}>{t("app.name")}</Text>
      </View>
      <View style={styles.card}>
        <Text style={textStyles.title} accessibilityRole="header">
          {t("signIn.title")}
        </Text>
        <Text style={textStyles.body}>{t("signIn.message")}</Text>
        <View style={styles.notice} accessibilityRole="text">
          <Text style={textStyles.body}>{t("signIn.pending")}</Text>
        </View>
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  brand: { paddingTop: 36 },
  brandText: { color: colors.primary, fontSize: 24, fontWeight: "800" },
  card: {
    padding: 24,
    gap: 16,
    borderRadius: 20,
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
  },
  notice: {
    padding: 16,
    borderRadius: 12,
    backgroundColor: colors.background,
  },
});
