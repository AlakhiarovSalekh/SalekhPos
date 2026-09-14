import { useRouter } from "expo-router";
import { StyleSheet, Text, View } from "react-native";

import { AppButton, Screen, colors, textStyles } from "@/components/primitives";
import { ScreenHeader } from "@/components/operations";
import { useLocalization } from "@/localization/LocalizationProvider";
import { useWorkspace } from "@/state/workspace";

export function OrganizationsScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const { workspace } = useWorkspace();
  return <Screen>
    <ScreenHeader title={t("organizations.title")} onBack={() => router.back()} />
    <Text style={textStyles.body}>{t("organizations.explanation")}</Text>
    <View style={styles.card}>
      <Text style={textStyles.heading}>{t("organizations.current")}</Text>
      <Text selectable style={styles.identifier}>{workspace.organizationId}</Text>
      <Text style={textStyles.body}>{workspace.branch === null ? t("workspace.noBranch") : t("workspace.branch", { name: workspace.branch.name })}</Text>
    </View>
    <AppButton onPress={() => router.push("/stores")}>{t("organizations.chooseStore")}</AppButton>
  </Screen>;
}

const styles = StyleSheet.create({
  card: { padding: 18, gap: 10, borderWidth: 1, borderColor: colors.border, borderRadius: 14, backgroundColor: colors.surface },
  identifier: { color: colors.muted, fontSize: 13 },
});
