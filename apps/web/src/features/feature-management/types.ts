export type FeatureDecision=Readonly<{key:string;enabled:boolean;source:string;evaluatedAt:string}>;
export type FeatureOverride=Readonly<{organizationId:string;key:string;enabled:boolean;reason:string;updatedAt:string}>;
