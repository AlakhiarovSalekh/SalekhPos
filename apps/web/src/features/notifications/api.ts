import { getCsrfToken, requestJson } from "@/features/sales/api";
import { uuid } from "@/lib/boundedJson";
import { parseNotification, parseNotificationDeliveryPage, parseNotificationPage, parseNotificationPreferences } from "./parsers";
import type { NotificationDeliveryPage, NotificationDeliveryStatus, NotificationItem, NotificationPage, NotificationPreferences } from "./types";

const root = (organizationId: string) =>
  `/bff/api/v1/organizations/${uuid(organizationId, "organization")}`;

async function mutation<T>(url: string, body: unknown, parser: (value: unknown) => T, idempotent = false): Promise<T> {
  const csrf = await getCsrfToken();
  const headers: Record<string, string> = { "content-type": "application/json", "X-CSRF-TOKEN": csrf };
  if (idempotent) headers["Idempotency-Key"] = crypto.randomUUID();
  return requestJson(url, parser, { method: "POST", headers, body: JSON.stringify(body) });
}

export function getNotifications(organizationId: string, unreadOnly = false, signal?: AbortSignal): Promise<NotificationPage> {
  const query = new URLSearchParams({ pageSize: "100", unreadOnly: String(unreadOnly) });
  return requestJson(`${root(organizationId)}/notifications?${query}`, parseNotificationPage, signal ? { signal } : undefined);
}


export function getNotificationDeliveries(
  organizationId: string,
  input: {
    status?: NotificationDeliveryStatus;
    channel?: "email" | "push";
    after?: string;
  } = {},
  signal?: AbortSignal
): Promise<NotificationDeliveryPage> {
  const query = new URLSearchParams({ pageSize: "25" });
  if (input.status) query.set("status", input.status);
  if (input.channel) query.set("channel", input.channel);
  if (input.after) query.set("after", uuid(input.after, "delivery cursor"));
  return requestJson(
    `${root(organizationId)}/notification-deliveries?${query}`,
    parseNotificationDeliveryPage,
    signal ? { signal } : undefined
  );
}

export function markNotificationRead(organizationId: string, notificationId: string): Promise<NotificationItem> {
  return mutation(`${root(organizationId)}/notifications/${uuid(notificationId, "notification")}/read`, {}, parseNotification);
}

export function getNotificationPreferences(organizationId: string, signal?: AbortSignal): Promise<NotificationPreferences> {
  return requestJson(`${root(organizationId)}/notification-preferences`, parseNotificationPreferences, signal ? { signal } : undefined);
}

export async function updateNotificationPreferences(
  organizationId: string,
  input: Pick<NotificationPreferences, "inAppEnabled" | "emailEnabled" | "pushEnabled">
): Promise<NotificationPreferences> {
  const csrf = await getCsrfToken();
  return requestJson(`${root(organizationId)}/notification-preferences`, parseNotificationPreferences, {
    method: "PUT",
    headers: { "content-type": "application/json", "X-CSRF-TOKEN": csrf },
    body: JSON.stringify(input)
  });
}

export function createNotification(organizationId: string, input: {
  branchId?: string;
  title: string;
  body: string;
  severity: "info" | "warning" | "critical";
}): Promise<NotificationItem> {
  return mutation(`${root(organizationId)}/notifications`, input, parseNotification, true);
}
