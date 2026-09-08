"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

type Session = { configured: boolean; authenticated: boolean; name: string | null; csrfToken: string | null };

export function SessionPanel({ dashboard = false }: { dashboard?: boolean }) {
  const [session, setSession] = useState<Session | null>(null);
  const [error, setError] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    fetch("/auth/session", { cache: "no-store", credentials: "same-origin", signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error("Session unavailable");
        const value: unknown = await response.json();
        if (!value || typeof value !== "object" || !("configured" in value) || !("authenticated" in value)
          || typeof value.configured !== "boolean" || typeof value.authenticated !== "boolean") throw new Error("Invalid session");
        setSession(value as Session);
      }).catch(() => { if (!controller.signal.aborted) setError(true); });
    return () => controller.abort();
  }, []);

  if (error) return <div role="alert" className="notice"><h2>We couldn’t connect</h2><p>Your session could not be loaded. Please try again.</p><button className="button" onClick={() => window.location.reload()}>Try again</button></div>;
  if (!session) return <p role="status" className="quiet">Opening your workspace…</p>;
  if (!session.configured) return <div className="notice" role="status"><h2>Sign-in is not available yet</h2><p>This workspace is still being prepared. Please contact your administrator.</p><Link className="text-link" href="/">Back to home</Link></div>;
  if (session.authenticated) return <><p className="eyebrow">SIGNED IN</p><h1>{dashboard ? "Welcome back" : "You’re signed in"}<span className="accent">.</span></h1><p className="description">{session.name || "Your account"}</p><div className="notice"><h2>Your workspace</h2><p>Store access is assigned separately by your organization. Your account is ready for the next setup step.</p></div>{!dashboard && <Link className="button" href="/dashboard">Open workspace →</Link>}<form action="/auth/logout" method="post"><input type="hidden" name="__RequestVerificationToken" value={session.csrfToken ?? ""} /><button className="text-button" type="submit">Sign out</button></form></>;
  return <><p className="eyebrow">WELCOME TO SALEKHPOS</p><h1>Your business.<br />Your workspace<span className="accent">.</span></h1><p className="description">Sign in with your organization’s account to continue.</p><form action="/auth/login" method="post"><input type="hidden" name="__RequestVerificationToken" value={session.csrfToken ?? ""} /><button className="button" type="submit">Continue to sign in <span aria-hidden="true">→</span></button></form><p className="quiet">You’ll continue to your organization’s secure sign-in page.</p></>;
}
