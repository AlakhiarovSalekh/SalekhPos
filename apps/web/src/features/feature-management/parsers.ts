import{exactKeys,isoDate,object,text,uuid}from"@/lib/boundedJson";
import type{FeatureDecision,FeatureOverride}from"./types";
function bool(v:unknown,l:string){if(typeof v!=="boolean")throw new Error("Invalid "+l);return v}
export function parseFeatureDecision(v:unknown):FeatureDecision{const x=object(v,"feature decision");exactKeys(x,["key","enabled","source","evaluatedAt"]);return{key:text(x.key,"key",128,1),enabled:bool(x.enabled,"enabled"),source:text(x.source,"source",64,1),evaluatedAt:isoDate(x.evaluatedAt,"evaluatedAt")}}
export function parseFeatureOverride(v:unknown):FeatureOverride{const x=object(v,"feature override");exactKeys(x,["organizationId","key","enabled","reason","updatedAt"]);return{organizationId:uuid(x.organizationId,"organizationId"),key:text(x.key,"key",128,1),enabled:bool(x.enabled,"enabled"),reason:text(x.reason,"reason",500,1),updatedAt:isoDate(x.updatedAt,"updatedAt")}}
