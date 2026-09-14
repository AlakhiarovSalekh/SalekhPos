import { Stack } from "expo-router";

import { permissions, hasPermission } from "@/permissions/policy";
import { useSession } from "@/state/SessionContext";
import { WorkspaceProvider } from "@/state/workspace";

export default function AppLayout() {
  const { session } = useSession();
  if (session === null) return null;
  const authorization = session.authorization;
  return (
    <WorkspaceProvider>
      <Stack screenOptions={{ headerShown: false }}>
        <Stack.Screen name="dashboard" />
        <Stack.Protected guard={hasPermission(authorization, permissions.branchesView)}>
          <Stack.Screen name="organizations" />
          <Stack.Screen name="stores" />
        </Stack.Protected>
        <Stack.Protected guard={hasPermission(authorization, permissions.productsView)}>
          <Stack.Screen name="products" />
          <Stack.Screen name="scanner" />
        </Stack.Protected>
        <Stack.Protected guard={hasPermission(authorization, permissions.inventoryView)}>
          <Stack.Screen name="inventory" />
        </Stack.Protected>
        <Stack.Protected guard={hasPermission(authorization, permissions.inventoryAdjust)}>
          <Stack.Screen name="inventory-receipt" />
          <Stack.Screen name="inventory-adjustment" />
        </Stack.Protected>
      </Stack>
    </WorkspaceProvider>
  );
}
