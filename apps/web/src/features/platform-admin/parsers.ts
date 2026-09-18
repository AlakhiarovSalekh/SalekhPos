import{exactKeys,isoDate,object,text,uuid}from"@/lib/boundedJson";
import type{PlatformAuthority,SuperAdminRecord}from"./types";

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
