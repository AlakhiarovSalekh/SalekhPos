export type PlatformAuthority=Readonly<{isRoot:boolean;isSuperAdmin:boolean}>;
export type SuperAdminRecord=Readonly<{id:string;issuer:string;subject:string;isRoot:boolean;isActive:boolean;createdAt:string;revokedAt:string|null}>;
export type PlatformHealth=Readonly<{live:"healthy"|"unhealthy"|"unknown";ready:"healthy"|"unhealthy"|"unknown";checkedAt:string}>;

export type SuperAdminPage=Readonly<{items:readonly SuperAdminRecord[];nextCursor:string|null}>;
export type PlatformAuditRecord=Readonly<{operationId:string;action:string;actorSubject:string;targetKey:string;targetId:string;reason:string;traceId:string;recordedAt:string}>;
export type PlatformAuditPage=Readonly<{items:readonly PlatformAuditRecord[];nextCursor:string|null}>;
