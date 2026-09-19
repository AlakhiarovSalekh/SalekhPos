import { boundedArray, exactKeys, isoDate, object, optionalUuid, text, uuid } from "@/lib/boundedJson";
import type { NotificationDeliveryActivity, NotificationDeliveryPage, NotificationItem, NotificationPage, NotificationPreferences } from "./types";

function nullableDate(value: unknown, label: string): string | null {
  return value === null ? null : isoDate(value, label);
}

function bool(value: unknown, label: string): boolean {
  if (typeof value !== "boolean") throw new Error(`Invalid ${label}`);
  return value;
}

export function parseNotification(value: unknown): NotificationItem {
  const x = object(value, "notification");
  exactKeys(x, ["id", "branchId", "recipientSubject", "title", "body", "severity", "isRead", "createdAt", "readAt"]);
  const severity = text(x.severity, "severity", 16, 1);
  if (severity !== "info" && severity !== "warning" && severity !== "critical") throw new Error("Invalid severity");
  return { id: uuid(x.id, "notification id"), branchId: optionalUuid(x.branchId, "branch id"), recipientSubject: text(x.recipientSubject, "recipient", 256, 1),
    title: text(x.title, "title", 180, 1), body: text(x.body, "body", 4000, 1), severity,
    isRead: bool(x.isRead, "read status"), createdAt: isoDate(x.createdAt, "created at"), readAt: nullableDate(x.readAt, "read at") };
}

export function parseNotificationPage(value: unknown): NotificationPage {
  const x = object(value, "notification page");
  exactKeys(x, ["items", "nextCursor"]);
  return {
    items: boundedArray(x.items, "notification items", 100).map(parseNotification),
    nextCursor: x.nextCursor === null ? null : uuid(x.nextCursor, "notification cursor")
  };
}

export function parseNotificationPreferences(value: unknown): NotificationPreferences {
  const x = object(value, "notification preferences");
  exactKeys(x, ["inAppEnabled", "emailEnabled", "pushEnabled", "updatedAt"]);
  return {
    inAppEnabled: bool(x.inAppEnabled, "in-app preference"),
    emailEnabled: bool(x.emailEnabled, "email preference"),
    pushEnabled: bool(x.pushEnabled, "push preference"),
    updatedAt: isoDate(x.updatedAt, "preference update")
  };
}

function optionalText(value: unknown, label: string, maximum: number): string | null {
  return value === null ? null : text(value, label, maximum, 1);
}

function nonNegativeInteger(value: unknown, label: string): number {
  if (typeof value !== "number" || !Number.isSafeInteger(value) || value < 0 || value > 10) {
    throw new Error(`Invalid ${label}`);
  }
  return value;
}

export function parseNotificationDelivery(value: unknown): NotificationDeliveryActivity {
  const x = object(value, "notification delivery");
  exactKeys(x, [
    "id", "notificationId", "channel", "recipientSubject", "status", "attemptCount",
    "nextAttemptAt", "lastErrorCode", "createdAt", "updatedAt", "title", "severity"
  ]);
  const channel = text(x.channel, "delivery channel", 16, 1);
  if (channel !== "email" && channel !== "push") throw new Error("Invalid delivery channel");
  const status = text(x.status, "delivery status", 32, 1);
  if (!["pending", "delivering", "failed", "delivered", "dead_lettered"].includes(status)) {
    throw new Error("Invalid delivery status");
  }
  const severity = text(x.severity, "delivery severity", 16, 1);
  if (severity !== "info" && severity !== "warning" && severity !== "critical") {
    throw new Error("Invalid delivery severity");
  }
  return {
    id: uuid(x.id, "delivery id"),
    notificationId: uuid(x.notificationId, "delivery notification id"),
    channel,
    recipientSubject: text(x.recipientSubject, "delivery recipient", 256, 1),
    status: status as NotificationDeliveryActivity["status"],
    attemptCount: nonNegativeInteger(x.attemptCount, "delivery attempt count"),
    nextAttemptAt: nullableDate(x.nextAttemptAt, "delivery retry at"),
    lastErrorCode: optionalText(x.lastErrorCode, "delivery error code", 100),
    createdAt: isoDate(x.createdAt, "delivery created at"),
    updatedAt: isoDate(x.updatedAt, "delivery updated at"),
    title: text(x.title, "delivery title", 160, 1),
    severity,
  };
}

export function parseNotificationDeliveryPage(value: unknown): NotificationDeliveryPage {
  const x = object(value, "notification delivery page");
  exactKeys(x, ["items", "nextCursor"]);
  return {
    items: boundedArray(x.items, "notification delivery items", 100).map(parseNotificationDelivery),
    nextCursor: x.nextCursor === null ? null : uuid(x.nextCursor, "notification delivery cursor"),
  };
}
