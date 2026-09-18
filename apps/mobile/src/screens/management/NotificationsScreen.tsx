import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Text, View } from "react-native";

import { useApiClient } from "@/api/ApiContext";
import type {
  NotificationDeliveryActivity,
  NotificationDeliveryStatus,
  NotificationItem,
  NotificationPreferences,
} from "@/api/globalConfigurationContracts";
import { managerStyles } from "@/components/managerStyles";
import { EmptyState, ScreenHeader } from "@/components/operations";
import { AppButton, LoadingSurface, Screen, textStyles } from "@/components/primitives";
import { hasPermission, permissions } from "@/permissions/policy";
import { createGlobalConfiguration } from "@/services/globalConfiguration";
import { useSession } from "@/state/SessionContext";
import { useWorkspace } from "@/state/workspace";

const deliveryStatuses = [
  "all",
  "pending",
  "delivering",
  "failed",
  "delivered",
  "dead_lettered",
] as const;

const deliveryChannels = ["all", "email", "push"] as const;

type DeliveryStatusFilter = (typeof deliveryStatuses)[number];
type DeliveryChannelFilter = (typeof deliveryChannels)[number];

function nextValue<T extends string>(values: readonly T[], value: T): T {
  const first = values[0];
  if (first === undefined) throw new Error("Filter values are required.");
  const index = values.indexOf(value);
  return values[(index + 1) % values.length] ?? first;
}

function statusLabel(value: DeliveryStatusFilter): string {
  switch (value) {
    case "all":
      return "All";
    case "failed":
      return "Retry scheduled";
    case "dead_lettered":
      return "Dead-lettered";
    default:
      return value.charAt(0).toUpperCase() + value.slice(1);
  }
}

export function NotificationsScreen() {
  const router = useRouter();
  const client = useApiClient();
  const service = useMemo(() => createGlobalConfiguration(client), [client]);
  const { workspace } = useWorkspace();
  const { session } = useSession();
  const canManageDeliveries =
    session !== null && hasPermission(session.authorization, permissions.notificationsManage);

  const [items, setItems] = useState<readonly NotificationItem[]>([]);
  const [prefs, setPrefs] = useState<NotificationPreferences | null>(null);
  const [deliveries, setDeliveries] = useState<readonly NotificationDeliveryActivity[]>([]);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [deliveryStatus, setDeliveryStatus] = useState<DeliveryStatusFilter>("all");
  const [deliveryChannel, setDeliveryChannel] = useState<DeliveryChannelFilter>("all");
  const [busy, setBusy] = useState(false);
  const [deliveryBusy, setDeliveryBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [deliveryMessage, setDeliveryMessage] = useState("");

  const load = useCallback(
    async (signal?: AbortSignal) => {
      setBusy(true);
      try {
        const [page, preferences] = await Promise.all([
          service.listNotifications(workspace.organizationId, unreadOnly, signal),
          service.readNotificationPreferences(workspace.organizationId, signal),
        ]);
        setItems(page.items);
        setPrefs(preferences);
        setMessage("");
      } catch {
        if (signal?.aborted !== true) {
          setMessage("Notifications could not be loaded.");
        }
      } finally {
        if (signal?.aborted !== true) {
          setBusy(false);
        }
      }
    },
    [service, unreadOnly, workspace.organizationId],
  );

  const loadDeliveries = useCallback(
    async (signal?: AbortSignal) => {
      if (!canManageDeliveries) {
        setDeliveries([]);
        setDeliveryMessage("");
        return;
      }

      setDeliveryBusy(true);
      try {
        const page = await service.listNotificationDeliveries(
          workspace.organizationId,
          {
            ...(deliveryStatus === "all"
              ? {}
              : { status: deliveryStatus as NotificationDeliveryStatus }),
            ...(deliveryChannel === "all" ? {} : { channel: deliveryChannel }),
          },
          signal,
        );
        setDeliveries(page.items);
        setDeliveryMessage("");
      } catch {
        if (signal?.aborted !== true) {
          setDeliveries([]);
          setDeliveryMessage("External delivery activity could not be loaded.");
        }
      } finally {
        if (signal?.aborted !== true) {
          setDeliveryBusy(false);
        }
      }
    },
    [
      canManageDeliveries,
      deliveryChannel,
      deliveryStatus,
      service,
      workspace.organizationId,
    ],
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  useEffect(() => {
    const controller = new AbortController();
    void loadDeliveries(controller.signal);
    return () => controller.abort();
  }, [loadDeliveries]);

  async function markRead(item: NotificationItem) {
    setBusy(true);
    try {
      await service.markNotificationRead(workspace.organizationId, item.id);
      await load();
    } catch {
      setMessage("Notification could not be marked as read.");
    } finally {
      setBusy(false);
    }
  }

  async function save(next: NotificationPreferences) {
    setBusy(true);
    try {
      setPrefs(await service.updateNotificationPreferences(workspace.organizationId, next));
      setMessage("");
    } catch {
      setMessage("Notification preferences could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Screen>
      <ScreenHeader title="Notifications" onBack={() => router.back()} />
      <Text style={textStyles.body}>
        Review operational alerts, choose delivery channels, and inspect external delivery health.
      </Text>

      {message ? (
        <View style={managerStyles.card}>
          <Text style={managerStyles.muted}>{message}</Text>
        </View>
      ) : null}

      <View style={managerStyles.card}>
        <Text style={textStyles.heading}>Inbox</Text>
        <AppButton disabled={busy} onPress={() => setUnreadOnly((value) => !value)}>
          {unreadOnly ? "Show all" : "Unread only"}
        </AppButton>
      </View>

      {busy ? <LoadingSurface /> : null}
      {!busy && items.length === 0 ? <EmptyState message="No notifications were returned." /> : null}

      {items.map((item) => (
        <View key={item.id} style={managerStyles.card}>
          <Text style={managerStyles.strong}>{item.title}</Text>
          <Text style={managerStyles.muted}>
            {item.severity} · {new Date(item.createdAt).toLocaleString()}
          </Text>
          <Text style={managerStyles.muted}>{item.body}</Text>
          {!item.isRead ? (
            <AppButton disabled={busy} onPress={() => void markRead(item)}>
              Mark read
            </AppButton>
          ) : null}
        </View>
      ))}

      {prefs ? (
        <View style={managerStyles.card}>
          <Text style={textStyles.heading}>Preferences</Text>
          <AppButton
            disabled={busy}
            onPress={() => void save({ ...prefs, inAppEnabled: !prefs.inAppEnabled })}
          >
            In-app: {prefs.inAppEnabled ? "On" : "Off"}
          </AppButton>
          <AppButton
            disabled={busy}
            onPress={() => void save({ ...prefs, emailEnabled: !prefs.emailEnabled })}
          >
            Email: {prefs.emailEnabled ? "On" : "Off"}
          </AppButton>
          <AppButton
            disabled={busy}
            onPress={() => void save({ ...prefs, pushEnabled: !prefs.pushEnabled })}
          >
            Push: {prefs.pushEnabled ? "On" : "Off"}
          </AppButton>
        </View>
      ) : null}

      {canManageDeliveries ? (
        <>
          <View style={managerStyles.card}>
            <Text style={textStyles.heading}>External delivery activity</Text>
            <Text style={managerStyles.muted}>
              Provider credentials and raw destination addresses are never displayed here.
            </Text>
            <AppButton
              disabled={deliveryBusy}
              onPress={() =>
                setDeliveryStatus((value) => nextValue(deliveryStatuses, value))
              }
            >
              Status: {statusLabel(deliveryStatus)}
            </AppButton>
            <AppButton
              disabled={deliveryBusy}
              onPress={() =>
                setDeliveryChannel((value) => nextValue(deliveryChannels, value))
              }
            >
              Channel: {deliveryChannel === "all" ? "All" : deliveryChannel}
            </AppButton>
            <AppButton disabled={deliveryBusy} onPress={() => void loadDeliveries()}>
              Refresh delivery activity
            </AppButton>
          </View>

          {deliveryMessage ? (
            <View style={managerStyles.card}>
              <Text style={managerStyles.muted}>{deliveryMessage}</Text>
            </View>
          ) : null}

          {deliveryBusy ? <LoadingSurface /> : null}
          {!deliveryBusy && !deliveryMessage && deliveries.length === 0 ? (
            <EmptyState message="No external delivery activity matched the current filters." />
          ) : null}

          {deliveries.map((item) => (
            <View key={item.id} style={managerStyles.card}>
              <Text style={managerStyles.strong}>{item.title}</Text>
              <Text style={managerStyles.muted}>
                {item.channel} · {item.status} · attempts {item.attemptCount}
              </Text>
              <Text style={managerStyles.muted}>
                Recipient reference: {item.recipientSubject}
              </Text>
              <Text style={managerStyles.muted}>
                Updated {new Date(item.updatedAt).toLocaleString()}
              </Text>
              {item.nextAttemptAt ? (
                <Text style={managerStyles.muted}>
                  Retry {new Date(item.nextAttemptAt).toLocaleString()}
                </Text>
              ) : null}
              {item.lastErrorCode ? (
                <Text style={managerStyles.danger}>Error code: {item.lastErrorCode}</Text>
              ) : null}
            </View>
          ))}
        </>
      ) : null}
    </Screen>
  );
}
