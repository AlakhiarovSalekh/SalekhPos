import{describe,expect,it}from"vitest";
import{parsePlatformAuditPage,parsePlatformAuthority,parseSuperAdminPage,parseSuperAdminRecord}from"./parsers";
const id="11111111-1111-4111-8111-111111111111";
describe("platform admin parsers",()=>{
  it("parses authority",()=>expect(parsePlatformAuthority({isRoot:true,isSuperAdmin:true}).isRoot).toBe(true));
  it("rejects string booleans",()=>expect(()=>parsePlatformAuthority({isRoot:"true",isSuperAdmin:true})).toThrow());
  it("parses admin evidence",()=>expect(parseSuperAdminRecord({id,issuer:"https://issuer.test",subject:"root",isRoot:false,isActive:true,createdAt:"2026-09-18T10:00:00Z",revokedAt:null}).id).toBe(id));
  it("rejects malformed admin id",()=>expect(()=>parseSuperAdminRecord({id:"bad",issuer:"https://issuer.test",subject:"x",isRoot:false,isActive:true,createdAt:"2026-09-18T10:00:00Z",revokedAt:null})).toThrow());
  it("parses bounded admin page",()=>expect(parseSuperAdminPage({items:[{id,issuer:"https://issuer.test",subject:"root",isRoot:true,isActive:true,createdAt:"2026-09-18T10:00:00Z",revokedAt:null}],nextCursor:null}).items).toHaveLength(1));
  it("parses audit evidence",()=>expect(parsePlatformAuditPage({items:[{operationId:id,action:"super_admin.registered",actorSubject:"root",targetKey:"support",targetId:id,reason:"grant",traceId:"a".repeat(32),recordedAt:"2026-09-18T10:00:00Z"}],nextCursor:null}).items[0]?.action).toBe("super_admin.registered"));
});
