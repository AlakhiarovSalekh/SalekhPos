import assert from "node:assert/strict";
import test from "node:test";

import {
  ApiClient,
  ApiClientConfigurationError,
  ApiHttpError,
  ApiProblemError,
  ApiRequestAbortedError,
  ApiResponseParseError,
  ApiResponseTooLargeError,
  assertUuid,
  branchPath,
  createInMemoryBearerTokenProvider,
  encodeQuery,
  isUuid,
  normalizeApiBaseUrl,
  organizationPath,
} from "../src/index.js";

const ORGANIZATION_ID = "11111111-1111-4111-8111-111111111111";
const BRANCH_ID = "22222222-2222-4222-8222-222222222222";

function jsonResponse(value: unknown, init: ResponseInit = {}): Response {
  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json; charset=utf-8");
  return new Response(JSON.stringify(value), { ...init, headers });
}

test("sends an asynchronously provided bearer token without retaining it in request errors", async () => {
  let observedAuthorization: string | null = null;
  let calls = 0;
  const client = new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: {
      async getAccessToken() {
        calls += 1;
        return "secret-token";
      },
    },
    fetch: async (_input, init) => {
      observedAuthorization = new Headers(init?.headers).get("Authorization");
      return jsonResponse({ ok: true });
    },
  });

  assert.deepEqual(await client.get<{ ok: boolean }>("/api/v1/status"), { ok: true });
  assert.equal(calls, 1);
  assert.equal(observedAuthorization, "Bearer secret-token");
});

test("in-memory token provider can rotate and clear credentials", async () => {
  const provider = createInMemoryBearerTokenProvider("first");
  assert.equal(await provider.getAccessToken(), "first");
  provider.setAccessToken("second");
  assert.equal(await provider.getAccessToken(), "second");
  provider.clear();
  assert.equal(await provider.getAccessToken(), null);
});

test("omits authorization when the in-memory provider has been cleared", async () => {
  const provider = createInMemoryBearerTokenProvider("temporary");
  provider.clear();
  const client = new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: provider,
    fetch: async (_input, init) => {
      assert.equal(new Headers(init?.headers).has("Authorization"), false);
      return jsonResponse({ anonymous: true });
    },
  });

  assert.deepEqual(await client.get("/api/v1/status"), { anonymous: true });
});

test("builds canonical scoped paths and safely encoded deterministic queries", () => {
  assert.equal(
    organizationPath(ORGANIZATION_ID.toUpperCase(), "catalog", "a/b"),
    `/api/v1/organizations/${ORGANIZATION_ID}/catalog/a%2Fb`,
  );
  assert.equal(
    branchPath(ORGANIZATION_ID, BRANCH_ID, "sales", "open shift"),
    `/api/v1/organizations/${ORGANIZATION_ID}/branches/${BRANCH_ID}/sales/open%20shift`,
  );
  assert.equal(
    encodeQuery({ z: "last", empty: null, tag: ["a/b", "two words"], active: true, page: 2 }),
    "?active=true&page=2&tag=a%2Fb&tag=two%20words&z=last",
  );
});

test("validates UUIDs", () => {
  assert.equal(isUuid(ORGANIZATION_ID), true);
  assert.equal(isUuid("not-a-uuid"), false);
  assert.equal(assertUuid(ORGANIZATION_ID.toUpperCase()), ORGANIZATION_ID);
  assert.throws(() => branchPath(ORGANIZATION_ID, "not-a-uuid"), ApiClientConfigurationError);
});

test("accepts HTTPS origins and loopback HTTP but rejects unsafe base URLs", () => {
  assert.equal(normalizeApiBaseUrl("https://api.example.test"), "https://api.example.test/");
  assert.equal(normalizeApiBaseUrl("http://localhost:5000"), "http://localhost:5000/");
  assert.throws(() => normalizeApiBaseUrl("http://api.example.test"), ApiClientConfigurationError);
  assert.throws(() => normalizeApiBaseUrl("https://user:pass@api.example.test"), ApiClientConfigurationError);
  assert.throws(() => normalizeApiBaseUrl("https://api.example.test/base"), ApiClientConfigurationError);
});

test("rejects absolute, scheme-relative, traversal, query, and fragment request paths before fetch", async () => {
  let fetchCalled = false;
  const client = new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: createInMemoryBearerTokenProvider(),
    fetch: async () => {
      fetchCalled = true;
      return jsonResponse({});
    },
  });

  const unsafePaths = [
    "https://evil.example/api",
    "//evil.example/api",
    "/api/../admin",
    "/api/%2e%2e/admin",
    "/api/items?next=https://evil.example",
    "/api/items#fragment",
  ];
  for (const path of unsafePaths) {
    await assert.rejects(client.get(path), ApiClientConfigurationError);
  }
  assert.equal(fetchCalled, false);
});

test("uses stable JSON headers, request body, and idempotency key", async () => {
  const client = new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: createInMemoryBearerTokenProvider(),
    fetch: async (input, init) => {
      const headers = new Headers(init?.headers);
      assert.equal(String(input), "https://api.example.test/api/v1/sales");
      assert.equal(init?.method, "POST");
      assert.equal(headers.get("Accept"), "application/json");
      assert.equal(headers.get("Content-Type"), "application/json");
      assert.equal(headers.get("Idempotency-Key"), "sale-123");
      assert.equal(init?.body, '{"amount":12}');
      return jsonResponse({ id: "sale" }, { status: 201 });
    },
  });

  assert.deepEqual(
    await client.post<{ id: string }, { amount: number }>("/api/v1/sales", {
      body: { amount: 12 },
      idempotencyKey: "sale-123",
    }),
    { id: "sale" },
  );
});

test("dispatches typed PUT and PATCH requests through the same JSON transport", async () => {
  const methods: string[] = [];
  const client = new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: createInMemoryBearerTokenProvider(),
    fetch: async (_input, init) => {
      methods.push(init?.method ?? "");
      return jsonResponse({ updated: true });
    },
  });

  assert.deepEqual(await client.put<{ updated: boolean }>("/api/v1/items/1", { body: { name: "new" } }), {
    updated: true,
  });
  assert.deepEqual(await client.patch<{ updated: boolean }>("/api/v1/items/1", { body: { active: false } }), {
    updated: true,
  });
  assert.deepEqual(methods, ["PUT", "PATCH"]);
});

test("rejects a non-serializable JSON body before fetch", async () => {
  let fetchCalled = false;
  const client = new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: createInMemoryBearerTokenProvider(),
    fetch: async () => {
      fetchCalled = true;
      return jsonResponse({});
    },
  });
  const cyclic: { self?: unknown } = {};
  cyclic.self = cyclic;

  await assert.rejects(
    client.post("/api/v1/items", { body: cyclic }),
    (error: unknown) =>
      error instanceof ApiClientConfigurationError && !error.message.includes("cyclic"),
  );
  assert.equal(fetchCalled, false);
});

test("maps safe problem details without exposing raw body fields", async () => {
  const rawSecret = "database-password";
  const client = new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: createInMemoryBearerTokenProvider(),
    fetch: async () =>
      new Response(
        JSON.stringify({
          title: "Sale conflict",
          code: "sales.conflict",
          detail: rawSecret,
          stack: "internal-stack",
        }),
        { status: 409, headers: { "Content-Type": "application/problem+json" } },
      ),
  });

  const error = await client.get("/api/v1/sales/1").catch((value: unknown) => value);
  assert.ok(error instanceof ApiProblemError);
  assert.equal(error.status, 409);
  assert.equal(error.code, "sales.conflict");
  assert.equal(error.title, "Sale conflict");
  assert.equal(JSON.stringify(error).includes(rawSecret), false);
  assert.equal(error.message.includes(rawSecret), false);
  assert.deepEqual(Object.keys(error).sort(), ["code", "name", "status", "title"]);
});

test("uses a safe generic error for non-JSON and malformed problem responses", async (context) => {
  await context.test("non-JSON error", async () => {
    const client = new ApiClient({
      baseUrl: "https://api.example.test",
      tokenProvider: createInMemoryBearerTokenProvider(),
      fetch: async () => new Response("upstream secret", { status: 502, headers: { "Content-Type": "text/plain" } }),
    });
    await assert.rejects(client.get("/api/v1/status"), (error: unknown) =>
      error instanceof ApiHttpError && error.status === 502 && !error.message.includes("secret"),
    );
  });

  await context.test("malformed problem JSON", async () => {
    const client = new ApiClient({
      baseUrl: "https://api.example.test",
      tokenProvider: createInMemoryBearerTokenProvider(),
      fetch: async () => new Response("{bad", { status: 400, headers: { "Content-Type": "application/problem+json" } }),
    });
    await assert.rejects(client.get("/api/v1/status"), ApiHttpError);
  });
});

test("rejects malformed or non-JSON successful responses", async (context) => {
  for (const [name, response] of [
    ["malformed", new Response("{bad", { headers: { "Content-Type": "application/json" } })],
    ["non-JSON", new Response("ok", { headers: { "Content-Type": "text/plain" } })],
    ["empty", new Response(null, { status: 200, headers: { "Content-Type": "application/json" } })],
  ] as const) {
    await context.test(name, async () => {
      const client = new ApiClient({
        baseUrl: "https://api.example.test",
        tokenProvider: createInMemoryBearerTokenProvider(),
        fetch: async () => response,
      });
      await assert.rejects(client.get("/api/v1/status"), ApiResponseParseError);
    });
  }
});

test("rejects declared and streamed responses over the configured byte limit", async (context) => {
  await context.test("declared size", async () => {
    const client = new ApiClient({
      baseUrl: "https://api.example.test",
      tokenProvider: createInMemoryBearerTokenProvider(),
      maxResponseBytes: 4,
      fetch: async () => new Response("{}", { headers: { "Content-Type": "application/json", "Content-Length": "10" } }),
    });
    await assert.rejects(client.get("/api/v1/status"), ApiResponseTooLargeError);
  });

  await context.test("actual streamed size", async () => {
    const client = new ApiClient({
      baseUrl: "https://api.example.test",
      tokenProvider: createInMemoryBearerTokenProvider(),
      maxResponseBytes: 4,
      fetch: async () => jsonResponse({ long: true }),
    });
    await assert.rejects(client.get("/api/v1/status"), ApiResponseTooLargeError);
  });
});

test("returns undefined for 204 without attempting JSON parsing", async () => {
  const client = new ApiClient({
    baseUrl: "https://api.example.test",
    tokenProvider: createInMemoryBearerTokenProvider(),
    fetch: async () => new Response(null, { status: 204 }),
  });
  assert.equal(await client.delete("/api/v1/sales/1"), undefined);
});

test("maps cancellation before and during fetch to a stable abort error", async (context) => {
  await context.test("already aborted", async () => {
    const controller = new AbortController();
    controller.abort();
    let fetchCalled = false;
    const client = new ApiClient({
      baseUrl: "https://api.example.test",
      tokenProvider: createInMemoryBearerTokenProvider(),
      fetch: async () => {
        fetchCalled = true;
        return jsonResponse({});
      },
    });
    await assert.rejects(client.get("/api/v1/status", { signal: controller.signal }), ApiRequestAbortedError);
    assert.equal(fetchCalled, false);
  });

  await context.test("fetch aborted", async () => {
    const controller = new AbortController();
    const client = new ApiClient({
      baseUrl: "https://api.example.test",
      tokenProvider: createInMemoryBearerTokenProvider(),
      fetch: async () => {
        controller.abort();
        throw new DOMException("secret abort reason", "AbortError");
      },
    });
    await assert.rejects(client.get("/api/v1/status", { signal: controller.signal }), (error: unknown) =>
      error instanceof ApiRequestAbortedError && !error.message.includes("secret"),
    );
  });
});
