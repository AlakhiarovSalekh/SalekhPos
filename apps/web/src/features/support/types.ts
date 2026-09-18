export type SupportPriority = "low" | "normal" | "high" | "urgent";
export type SupportStatus = "open" | "in_progress" | "waiting_for_customer" | "resolved" | "closed";

export type SupportTicket = Readonly<{
  id: string;
  branchId: string | null;
  subject: string;
  description: string;
  priority: SupportPriority;
  status: SupportStatus;
  version: number;
  openedBySubject: string;
  createdAt: string;
  updatedAt: string;
}>;

export type SupportTicketPage = Readonly<{
  items: readonly SupportTicket[];
  nextCursor: string | null;
}>;

export type DiagnosticReference = Readonly<{
  id: string;
  ticketId: string;
  kind: string;
  reference: string;
  sha256: string;
  addedBySubject: string;
  createdAt: string;
}>;

export type SupportTicketDetail = Readonly<{
  ticket: SupportTicket;
  diagnostics: readonly DiagnosticReference[];
}>;
