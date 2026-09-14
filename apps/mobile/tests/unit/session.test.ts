import { describe, expect, it } from "vitest";

import {
  deserializeSession,
  getSessionValidity,
  InvalidSessionError,
  parseSession,
  serializeSession,
} from "../../src/security/session";

const session = {
  schemaVersion: 1,
  accessToken: "access-token",
  refreshToken: "refresh-token",
  accessTokenExpiresAt: 2_000_000_000,
  subject: "user-1",
  displayName: "Operator",
  organizationId: "11111111-1111-4111-8111-111111111111",
  branchId: "22222222-2222-4222-8222-222222222222",
  authorization: {
    roles: ["manager", "manager"],
    permissions: ["permission.read", "permission.read"],
  },
} as const;

describe("session", () => {
  it("validates, deduplicates, and round-trips a session envelope", () => {
    const parsed = parseSession(session);
    expect(parsed.authorization.roles).toEqual(["manager"]);
    expect(parsed.authorization.permissions).toEqual(["permission.read"]);
    expect(deserializeSession(serializeSession(parsed))).toEqual(parsed);
  });

  it("rejects malformed or unsupported stored sessions", () => {
    expect(() => parseSession({ ...session, schemaVersion: 2 })).toThrow(InvalidSessionError);
    expect(() => parseSession({ ...session, accessToken: "" })).toThrow(InvalidSessionError);
    expect(() => deserializeSession("not-json")).toThrow(InvalidSessionError);
  });

  it.each([
    { organizationId: "organization-1" },
    { organizationId: "00000000-0000-0000-0000-000000000000" },
    { branchId: "branch-1" },
    { subject: " user " },
    { accessToken: `token\nvalue` },
    { accessToken: "x".repeat(16_385) },
  ])("rejects unsafe identity and tenant session fields: %o", (change) => {
    expect(() => parseSession({ ...session, ...change })).toThrow(InvalidSessionError);
  });

  it("bounds authorization data and canonicalizes tenant UUID casing", () => {
    expect(parseSession({ ...session, organizationId: session.organizationId.toUpperCase() }).organizationId).toBe(session.organizationId);
    expect(() => parseSession({ ...session, authorization: { roles: Array(257).fill("role"), permissions: [] } })).toThrow(InvalidSessionError);
    expect(() => parseSession({ ...session, authorization: { roles: [], permissions: ["unsafe\npermission"] } })).toThrow(InvalidSessionError);
  });

  it("expires access tokens with a defensive clock skew", () => {
    expect(getSessionValidity({ accessTokenExpiresAt: 100 }, 60_000)).toBe("valid");
    expect(getSessionValidity({ accessTokenExpiresAt: 100 }, 70_000)).toBe("expired");
  });
});
