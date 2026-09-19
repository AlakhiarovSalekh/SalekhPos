import { ApiClient } from "@salekhpos/packages-api-client";
import { describe, expect, it, vi } from "vitest";

import {
  GlobalConfigurationContractError,
  parseNotificationDelivery,
} from "../../src/api/globalConfigurationContracts";
import { createGlobalConfiguration } from "../../src/services/globalConfiguration";

const organizationId = "11111111-1111-4111-8111-111111111111";
const deliveryId = "22222222-2222-4222-8222-222222222222";
const notificationId = "33333333-3333-4333-8333-333333333333";

const delivery = {
  id: deliveryId,
  notificationId,
  channel: "email",
  recipientSubject: "recipient-subject",
  status: "failed",
  attemptCount: 2,
  nextAttemptAt: "2026-09-19T02:00:00Z",
  lastErrorCode: "http_503",
  createdAt: "2026-09-19T01:00:00Z",
  updatedAt: "2026-09-19T01:05:00Z",
  title: "Inventory alert",
  severity: "warning",
} as const;

function json(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "content-type": "application/json" },
  });
}

function client(fetchImplementation: typeof fetch): ApiClient {
  return new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: {
      async getAccessToken() {
        return "signed-access-token";
      },
    },
    fetch: fetchImplementation,
  });
}

describe("mobile notification delivery configuration", () => {
  it("parses the exact bounded external delivery contract", () => {
    const parsed = parseNotificationDelivery(delivery);
    expect(parsed.id).toBe(deliveryId);
    expect(parsed.channel).toBe("email");
    expect(parsed.status).toBe("failed");
    expect(parsed.attemptCount).toBe(2);
  });

  it("rejects unknown fields and invalid delivery states", () => {
    expect(() =>
      parseNotificationDelivery({ ...delivery, providerSecret: "hidden" }),
    ).toThrow(GlobalConfigurationContractError);
    expect(() =>
      parseNotificationDelivery({ ...delivery, status: "unknown" }),
    ).toThrow(GlobalConfigurationContractError);
    expect(() =>
      parseNotificationDelivery({ ...delivery, attemptCount: 11 }),
    ).toThrow(GlobalConfigurationContractError);
  });

  it("retries a dead-lettered delivery with an idempotency key and audit reason", async () => {
    const retried = {
      ...delivery,
      status: "pending",
      attemptCount: 0,
      nextAttemptAt: null,
      lastErrorCode: null,
    } as const;
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(json(retried));
    const service = createGlobalConfiguration(
      client(fetchMock as unknown as typeof fetch),
    );

    const result = await service.retryNotificationDelivery(
      organizationId,
      deliveryId,
      "  Provider configuration corrected.  ",
    );

    expect(result.status).toBe("pending");
    expect(result.attemptCount).toBe(0);
    const [rawUrl, init] = fetchMock.mock.calls[0] ?? [];
    const url = new URL(String(rawUrl));
    expect(url.pathname).toBe(
      `/api/v1/organizations/${organizationId}/notifications/deliveries/${deliveryId}/retry`,
    );
    expect(init?.method).toBe("POST");
    const headers = new Headers(init?.headers);
    expect(headers.get("authorization")).toBe("Bearer signed-access-token");
    expect(headers.get("idempotency-key")).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/u,
    );
    expect(JSON.parse(String(init?.body))).toEqual({
      reason: "Provider configuration corrected.",
    });
  });

  it("uses the authenticated tenant-scoped delivery activity endpoint and filters", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      json({ items: [delivery], nextCursor: null }),
    );
    const service = createGlobalConfiguration(
      client(fetchMock as unknown as typeof fetch),
    );

    const page = await service.listNotificationDeliveries(organizationId, {
      status: "failed",
      channel: "email",
    });

    expect(page.items).toHaveLength(1);
    const [rawUrl, init] = fetchMock.mock.calls[0] ?? [];
    const url = new URL(String(rawUrl));
    expect(url.pathname).toBe(
      `/api/v1/organizations/${organizationId}/notifications/deliveries`,
    );
    expect(url.searchParams.get("pageSize")).toBe("25");
    expect(url.searchParams.get("status")).toBe("failed");
    expect(url.searchParams.get("channel")).toBe("email");
    expect(new Headers(init?.headers).get("authorization")).toBe(
      "Bearer signed-access-token",
    );
  });
});
