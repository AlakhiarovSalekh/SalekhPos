import { describe, expect, it } from "vitest";

import {
  parseNotificationDelivery,
  parseNotificationDeliveryPage,
} from "./parsers";

const id = (suffix: string) =>
  `10000000-0000-0000-0000-${suffix.padStart(12, "0")}`;

const delivery = {
  id: id("1"),
  notificationId: id("2"),
  channel: "email",
  recipientSubject: "subject-123",
  status: "failed",
  attemptCount: 2,
  nextAttemptAt: "2026-09-19T01:30:00Z",
  lastErrorCode: "http_503",
  createdAt: "2026-09-19T01:00:00Z",
  updatedAt: "2026-09-19T01:05:00Z",
  title: "Stock alert",
  severity: "warning",
} as const;

describe("notification delivery parsers", () => {
  it("accepts the exact external delivery activity contract", () => {
    const parsed = parseNotificationDelivery(delivery);
    expect(parsed.channel).toBe("email");
    expect(parsed.status).toBe("failed");
    expect(parsed.attemptCount).toBe(2);
    expect(parsed.lastErrorCode).toBe("http_503");
  });

  it("accepts bounded delivery pages", () => {
    const page = parseNotificationDeliveryPage({
      items: [delivery],
      nextCursor: id("9"),
    });
    expect(page.items).toHaveLength(1);
    expect(page.nextCursor).toBe(id("9"));
  });

  it("rejects unknown status, unsafe attempt count, and response expansion", () => {
    expect(() =>
      parseNotificationDelivery({ ...delivery, status: "unknown" }),
    ).toThrow();
    expect(() =>
      parseNotificationDelivery({ ...delivery, attemptCount: 11 }),
    ).toThrow();
    expect(() =>
      parseNotificationDelivery({ ...delivery, providerSecret: "no" }),
    ).toThrow();
  });
});
