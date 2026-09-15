import { StyleSheet } from "react-native";
import { colors } from "./primitives";

export const managerStyles = StyleSheet.create({
  section: { gap: 12 },
  card: { padding: 18, gap: 8, borderRadius: 14, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.surface },
  row: { flexDirection: "row", flexWrap: "wrap", gap: 12, alignItems: "center" },
  field: { gap: 6 },
  label: { color: colors.text, fontSize: 13, fontWeight: "700" },
  input: { minHeight: 48, borderWidth: 1, borderColor: colors.border, borderRadius: 12, backgroundColor: colors.surface, color: colors.text, paddingHorizontal: 14, fontSize: 16 },
  mono: { color: colors.text, fontFamily: "monospace", fontSize: 12 },
  strong: { color: colors.text, fontSize: 17, fontWeight: "700" },
  muted: { color: colors.muted, fontSize: 14, lineHeight: 20 },
  warning: { padding: 16, borderRadius: 12, borderWidth: 1, borderColor: "#DFC46A", backgroundColor: "#FFF7D6", gap: 6 },
  success: { padding: 16, borderRadius: 12, borderWidth: 1, borderColor: "#9FCDB5", backgroundColor: "#E8F5ED", gap: 6 },
  danger: { color: colors.danger, fontSize: 14, lineHeight: 20 },
});
