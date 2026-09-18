export type PlatformAuthority=Readonly<{isRoot:boolean;isSuperAdmin:boolean}>;
export type SuperAdminRecord=Readonly<{id:string;issuer:string;subject:string;isRoot:boolean;isActive:boolean;createdAt:string;revokedAt:string|null}>;
export type PlatformHealth=Readonly<{live:"healthy"|"unhealthy"|"unknown";ready:"healthy"|"unhealthy"|"unknown";checkedAt:string}>;
