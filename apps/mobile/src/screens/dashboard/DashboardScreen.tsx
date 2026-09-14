import { useRouter } from "expo-router";
import { Pressable, StyleSheet, Text, View } from "react-native";

import { AppButton, Screen, colors, textStyles } from "@/components/primitives";
import { useLocalization } from "@/localization/LocalizationProvider";
import {
  getVisibleNavigationItems,
  mobileNavigationItems,
} from "@/navigation/model";
import { useSession } from "@/state/SessionContext";

export function DashboardScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const { session, signOut } = useSession();

  if (session === null) {
    return null;
  }

  const items = getVisibleNavigationItems(mobileNavigationItems, session.authorization).filter(
    (item) => item.route !== "/dashboard",
  );
  const name = session.displayName ?? session.subject;

  return (
    <Screen>
      <View style={styles.header}>
        <View style={styles.headerText}>
          <Text style={textStyles.title} accessibilityRole="header">
            {t("dashboard.title")}
          </Text>
          <Text style={textStyles.body}>{t("dashboard.welcome", { name })}</Text>
        </View>
        <AppButton onPress={() => void signOut()} accessibilityLabel={t("common.signOut")}>
          {t("common.signOut")}
        </AppButton>
      </View>

      <View style={styles.card}>
        <Text style={textStyles.heading}>{t("dashboard.roles")}</Text>
        <Text style={textStyles.body}>
          {session.authorization.roles.length === 0
            ? t("dashboard.noRoles")
            : session.authorization.roles.join(", ")}
        </Text>
      </View>

      <View style={styles.navigation} accessibilityRole="menu">
        {items.map((item) => (
          <Pressable
            key={item.id}
            accessibilityRole="menuitem"
            accessibilityLabel={t(item.labelKey)}
            onPress={() => router.push(item.route)}
            style={({ pressed }) => [styles.navigationCard, pressed && styles.pressed]}
          >
            <Text style={textStyles.heading}>{t(item.labelKey)}</Text>
          </Pressable>
        ))}
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: "row", flexWrap: "wrap", gap: 16, alignItems: "center" },
  headerText: { flexGrow: 1, flexShrink: 1, gap: 4 },
  card: {
    padding: 20,
    gap: 8,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
  },
  navigation: { gap: 12 },
  navigationCard: {
    minHeight: 64,
    padding: 20,
    justifyContent: "center",
    borderRadius: 16,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
  },
  pressed: { opacity: 0.75 },
});
