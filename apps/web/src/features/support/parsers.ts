import { boundedArray, exactKeys, integer, isoDate, object, optionalUuid, text, uuid } from "@/lib/boundedJson";
import type { DiagnosticReference, SupportPriority, SupportStatus, SupportTicket, SupportTicketDetail, SupportTicketPage } from "./types";

const priorities = new Set<SupportPriority>(["low", "normal", "high", "urgent"]);
const statuses = new Set<SupportStatus>(["open", "in_progress", "waiting_for_customer", "resolved", "closed"]);

function multilineText(value: unknown, label: string, maximum: number, minimum = 1): string {
  if (typeof value !== "string" || value.length < minimum || value.length > maximum || value.trim() !== value
      || [...value].some(character => character < " " && character !== "\r" && character !== "\n" && character !== "\t")) {
    throw new Error(`Invalid ${label}`);
  }
  return value;
}

function priority(value: unknown): SupportPriority {
  const result = text(value, "support priority", 16, 3) as SupportPriority;
  if (!priorities.has(result)) throw new Error("Invalid support priority");
  return result;
}

function status(value: unknown): SupportStatus {
  const result = text(value, "support status", 32, 4) as SupportStatus;
  if (!statuses.has(result)) throw new Error("Invalid support status");
  return result;
}

export function parseSupportTicket(value: unknown): SupportTicket {
  const x = object(value, "support ticket");
  exactKeys(x, ["id", "branchId", "subject", "description", "priority", "status", "version",
    "openedBySubject", "createdAt", "updatedAt"]);
  return {
    id: uuid(x.id, "ticket id"),
    branchId: optionalUuid(x.branchId, "branch id"),
    subject: multilineText(x.subject, "subject", 200),
    description: multilineText(x.description, "description", 8000),
    priority: priority(x.priority),
    status: status(x.status),
    version: integer(x.version, "ticket version", 1, 2_147_483_647),
    openedBySubject: text(x.openedBySubject, "opened by subject", 256, 1),
    createdAt: isoDate(x.createdAt, "created at"),
    updatedAt: isoDate(x.updatedAt, "updated at"),
  };
}

export function parseSupportTicketPage(value: unknown): SupportTicketPage {
  const x = object(value, "support ticket page");
  exactKeys(x, ["items", "nextCursor"]);
  return {
    items: boundedArray(x.items, "support tickets", 100).map(parseSupportTicket),
    nextCursor: x.nextCursor === null ? null : uuid(x.nextCursor, "support cursor"),
  };
}

export function parseDiagnosticReference(value: unknown): DiagnosticReference {
  const x = object(value, "diagnostic reference");
  exactKeys(x, ["id", "ticketId", "kind", "reference", "sha256", "addedBySubject", "createdAt"]);
  const digest = text(x.sha256, "diagnostic digest", 64, 64).toLowerCase();
  if (!/^[0-9a-f]{64}$/u.test(digest)) throw new Error("Invalid diagnostic digest");
  return {
    id: uuid(x.id, "diagnostic id"),
    ticketId: uuid(x.ticketId, "ticket id"),
    kind: multilineText(x.kind, "diagnostic kind", 64),
    reference: multilineText(x.reference, "diagnostic reference", 512),
    sha256: digest,
    addedBySubject: text(x.addedBySubject, "diagnostic author", 256, 1),
    createdAt: isoDate(x.createdAt, "diagnostic created at"),
  };
}

export function parseSupportTicketDetail(value: unknown): SupportTicketDetail {
  const x = object(value, "support ticket detail");
  exactKeys(x, ["ticket", "diagnostics"]);
  const ticket = parseSupportTicket(x.ticket);
  const diagnostics = boundedArray(x.diagnostics, "diagnostics", 100).map(parseDiagnosticReference);
  if (diagnostics.some(item => item.ticketId !== ticket.id)) throw new Error("Cross-ticket diagnostic response");
  return { ticket, diagnostics };
}
