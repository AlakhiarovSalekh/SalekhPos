import{exactKeys,isoDate,object,text,uuid}from"@/lib/boundedJson";
import type{PlatformAuditPage,PlatformAuditRecord,PlatformAuthority,SuperAdminPage,SuperAdminRecord}from"./types";

function bool(v:unknown,l:string){if(typeof v!=="boolean")throw new Error("Invalid "+l);return v}
export function parsePlatformAuthority(v:unknown):PlatformAuthority{
  const x=object(v,"platform authority");exactKeys(x,["isRoot","isSuperAdmin"]);
  return{isRoot:bool(x.isRoot,"isRoot"),isSuperAdmin:bool(x.isSuperAdmin,"isSuperAdmin")}
}
export function parseSuperAdminRecord(v:unknown):SuperAdminRecord{
  const x=object(v,"super admin");exactKeys(x,["id","issuer","subject","isRoot","isActive","createdAt","revokedAt"]);
  return{id:uuid(x.id,"id"),issuer:text(x.issuer,"issuer",2048,1),subject:text(x.subject,"subject",256,1),
    isRoot:bool(x.isRoot,"isRoot"),isActive:bool(x.isActive,"isActive"),createdAt:isoDate(x.createdAt,"createdAt"),
    revokedAt:x.revokedAt===null?null:isoDate(x.revokedAt,"revokedAt")}
}

export function parseSuperAdminPage(v:unknown):SuperAdminPage{
  const x=object(v,"super admin page");exactKeys(x,["items","nextCursor"]);
  if(!Array.isArray(x.items)||x.items.length>100)throw new Error("Invalid super admin page");
  return{items:x.items.map(parseSuperAdminRecord),nextCursor:x.nextCursor===null?null:uuid(x.nextCursor,"nextCursor")}
}
export function parsePlatformAuditRecord(v:unknown):PlatformAuditRecord{
  const x=object(v,"platform audit");exactKeys(x,["operationId","action","actorSubject","targetKey","targetId","reason","traceId","recordedAt"]);
  const trace=text(x.traceId,"traceId",32,32);if(!/^[0-9a-f]{32}$/iu.test(trace))throw new Error("Invalid trace id");
  return{operationId:uuid(x.operationId,"operationId"),action:text(x.action,"action",64,1),actorSubject:text(x.actorSubject,"actorSubject",256,1),
    targetKey:text(x.targetKey,"targetKey",256,1),targetId:uuid(x.targetId,"targetId"),reason:text(x.reason,"reason",1000,1),
    traceId:trace.toLowerCase(),recordedAt:isoDate(x.recordedAt,"recordedAt")}
}
export function parsePlatformAuditPage(v:unknown):PlatformAuditPage{
  const x=object(v,"platform audit page");exactKeys(x,["items","nextCursor"]);
  if(!Array.isArray(x.items)||x.items.length>100)throw new Error("Invalid platform audit page");
  return{items:x.items.map(parsePlatformAuditRecord),nextCursor:x.nextCursor===null?null:uuid(x.nextCursor,"nextCursor")}
}
