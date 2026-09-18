import { describe, expect, it } from "vitest";
import { parseDiagnosticReference, parseSupportTicket, parseSupportTicketDetail, parseSupportTicketPage } from "./parsers";

const ticketId = "11111111-1111-4111-8111-111111111111";
const branchId = "22222222-2222-4222-8222-222222222222";
const diagnosticId = "33333333-3333-4333-8333-333333333333";
const ticket = { id: ticketId, branchId, subject: "Printer unavailable", description: "Receipt printer is offline.",
  priority: "high", status: "in_progress", version: 2, openedBySubject: "operator-1",
  createdAt: "2026-09-18T08:00:00Z", updatedAt: "2026-09-18T09:00:00Z" };

describe("support response parsers", () => {
  it("parses ticket pages and details", () => {
    expect(parseSupportTicket(ticket).status).toBe("in_progress");
    expect(parseSupportTicketPage({ items: [ticket], nextCursor: null }).items).toHaveLength(1);
    const diagnostic = { id: diagnosticId, ticketId, kind: "log", reference: "object://diag/1",
      sha256: "a".repeat(64), addedBySubject: "operator-2", createdAt: "2026-09-18T09:10:00Z" };
    expect(parseDiagnosticReference(diagnostic).sha256).toBe("a".repeat(64));
    expect(parseSupportTicketDetail({ ticket, diagnostics: [diagnostic] }).diagnostics).toHaveLength(1);
  });

  it("rejects unknown workflow values and cross-ticket diagnostics", () => {
    expect(() => parseSupportTicket({ ...ticket, status: "deleted" })).toThrow();
    expect(() => parseSupportTicket({ ...ticket, priority: "blocker" })).toThrow();
    const diagnostic = { id: diagnosticId, ticketId: branchId, kind: "log", reference: "object://diag/1",
      sha256: "a".repeat(64), addedBySubject: "operator-2", createdAt: "2026-09-18T09:10:00Z" };
    expect(() => parseSupportTicketDetail({ ticket, diagnostics: [diagnostic] })).toThrow();
  });

  it("rejects malformed digests and oversized pages", () => {
    expect(() => parseDiagnosticReference({ id: diagnosticId, ticketId, kind: "log", reference: "object://diag/1",
      sha256: "not-a-digest", addedBySubject: "operator-2", createdAt: "2026-09-18T09:10:00Z" })).toThrow();
    expect(() => parseSupportTicketPage({ items: Array(101).fill(ticket), nextCursor: null })).toThrow();
  });
});
