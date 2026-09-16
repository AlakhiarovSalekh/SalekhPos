import { boundedArray, exactKeys, isoDate, object, optionalUuid, text, uuid } from "@/lib/boundedJson";
import type { NotificationItem, NotificationPage, NotificationPreferences } from "./types";

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
