import { boundedArray, exactKeys, integer, isoDate, object, optionalUuid, text, uuid } from "@/lib/boundedJson";
import type { AuditEvent, AuditEventPage, AuditIntegrity } from "./types";

function nullableText(value: unknown, label: string, max: number): string | null {
  return value === null ? null : text(value, label, max, 1);
}
function nullableSequence(value: unknown, label: string): number | null {
  return value === null ? null : integer(value, label, 1, Number.MAX_SAFE_INTEGER);
}
export function parseAuditEvent(value: unknown): AuditEvent {
  const x = object(value, "audit event");
  exactKeys(x,["id","sequence","actorSubject","action","targetType","targetId","branchId","deviceId","sourceIp","outcome","reason","correlationId","requestId","occurredAt","previousHash","eventHash"]);
  return { id: uuid(x.id,"id"), sequence: integer(x.sequence,"sequence",1,Number.MAX_SAFE_INTEGER),
    actorSubject: text(x.actorSubject,"actor",256,1), action: text(x.action,"action",120,1), targetType: text(x.targetType,"target",80,1),
    targetId: optionalUuid(x.targetId,"target id"), branchId: optionalUuid(x.branchId,"branch id"), deviceId: optionalUuid(x.deviceId,"device id"),
    sourceIp: nullableText(x.sourceIp,"source ip",80), outcome: text(x.outcome,"outcome",32,1), reason: nullableText(x.reason,"reason",500),
    correlationId: text(x.correlationId,"correlation",128,1), requestId: text(x.requestId,"request",128,1), occurredAt: isoDate(x.occurredAt,"occurred"),
    previousHash: text(x.previousHash,"previous hash",64,64), eventHash: text(x.eventHash,"event hash",64,64) };
}
export function parseAuditPage(value: unknown): AuditEventPage {
  const x = object(value,"audit page"); exactKeys(x,["items","nextSequence"]);
  return { items: boundedArray(x.items,"items",100).map(parseAuditEvent),
    nextSequence: nullableSequence(x.nextSequence,"next sequence") };
}
export function parseAuditIntegrity(value: unknown): AuditIntegrity {
  const x = object(value,"audit integrity");
  exactKeys(x,["isValid","verifiedEvents","firstSequence","lastSequence","lastHash"]);
  if (typeof x.isValid !== "boolean") throw new Error("Invalid audit integrity status");
  return { isValid:x.isValid, verifiedEvents:integer(x.verifiedEvents,"verified",0,Number.MAX_SAFE_INTEGER),
    firstSequence:nullableSequence(x.firstSequence,"first sequence"), lastSequence:nullableSequence(x.lastSequence,"last sequence"),
    lastHash:nullableText(x.lastHash,"last hash",64) };
}
