import Link from "next/link";
import type { ApiError } from "../api";

export function LoadingPanel({ label }: { label: string }) { return <div className="state-panel" role="status"><span className="spinner" aria-hidden="true" /><p>{label}</p></div>; }
export function EmptyPanel({ title, detail }: { title: string; detail: string }) { return <div className="state-panel"><h2>{title}</h2><p>{detail}</p></div>; }
export function ErrorPanel({ error, retry }: { error: ApiError; retry?: () => void }) {
  return <div className="state-panel error-panel" role="alert"><h2>{error.kind === "permission" ? "Access restricted" : error.kind === "authentication" ? "Sign-in required" : "Couldn’t load this view"}</h2><p>{error.message}</p>{error.kind === "authentication" ? <Link className="button compact" href="/sign-in">Sign in</Link> : retry && <button className="button compact" onClick={retry}>Try again</button>}</div>;
}
