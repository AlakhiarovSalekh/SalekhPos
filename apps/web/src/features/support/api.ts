import { getCsrfToken, requestJson } from "@/features/sales/api";
import { uuid } from "@/lib/boundedJson";
import { parseDiagnosticReference, parseSupportTicket, parseSupportTicketDetail, parseSupportTicketPage } from "./parsers";
import type { DiagnosticReference, SupportPriority, SupportStatus, SupportTicket, SupportTicketDetail, SupportTicketPage } from "./types";

const root = (organizationId: string) =>
  `/bff/api/v1/organizations/${uuid(organizationId, "organization id")}/support/tickets`;

function operation(value: string): string {
  return uuid(value, "operation id");
}

async function post<T>(url: string, body: unknown, operationId: string,
  parser: (value: unknown) => T): Promise<T> {
  const csrf = await getCsrfToken();
  return requestJson(url, parser, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "X-CSRF-TOKEN": csrf,
      "Idempotency-Key": operation(operationId),
    },
    body: JSON.stringify(body),
  });
}

export function listSupportTickets(organizationId: string, status?: SupportStatus | null,
  after?: string | null, signal?: AbortSignal): Promise<SupportTicketPage> {
  const query = new URLSearchParams({ pageSize: "50" });
  if (status) query.set("status", status);
  if (after) query.set("after", uuid(after, "support cursor"));
  return requestJson(`${root(organizationId)}?${query}`, parseSupportTicketPage,
    signal ? { signal } : undefined);
}

export function getSupportTicket(organizationId: string, ticketId: string,
  signal?: AbortSignal): Promise<SupportTicketDetail> {
  return requestJson(`${root(organizationId)}/${uuid(ticketId, "ticket id")}`,
    parseSupportTicketDetail, signal ? { signal } : undefined);
}

export function createSupportTicket(organizationId: string,
  input: { branchId: string | null; subject: string; description: string; priority: SupportPriority },
  operationId: string): Promise<SupportTicket> {
  return post(root(organizationId), {
    ...input,
    branchId: input.branchId ? uuid(input.branchId, "branch id") : null,
  }, operationId, parseSupportTicket);
}

export function transitionSupportTicket(organizationId: string, ticketId: string,
  input: { status: SupportStatus; expectedVersion: number; note: string },
  operationId: string): Promise<SupportTicket> {
  return post(`${root(organizationId)}/${uuid(ticketId, "ticket id")}/transitions`,
    input, operationId, parseSupportTicket);
}

export function addSupportDiagnostic(organizationId: string, ticketId: string,
  input: { kind: string; reference: string; sha256: string },
  operationId: string): Promise<DiagnosticReference> {
  return post(`${root(organizationId)}/${uuid(ticketId, "ticket id")}/diagnostics`,
    input, operationId, parseDiagnosticReference);
}
