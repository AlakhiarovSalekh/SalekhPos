"use client";
import { useCallback, useEffect, useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { createNotification, getNotifications, getNotificationPreferences, markNotificationRead, updateNotificationPreferences } from "./api";
import type { NotificationItem, NotificationPreferences } from "./types";

export function NotificationsWorkspace() {
  const scope = useOperationsScope();
  const [items, setItems] = useState<readonly NotificationItem[]>([]);
  const [preferences, setPreferences] = useState<NotificationPreferences | null>(null);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [severity, setSeverity] = useState<"info" | "warning" | "critical">("info");
  const load = useCallback(async (signal?: AbortSignal) => {
    if (!scope.organizationId) return;
    const [page, prefs] = await Promise.all([
      getNotifications(scope.organizationId, unreadOnly, signal),
      getNotificationPreferences(scope.organizationId, signal),
    ]);
    if (signal?.aborted) return;
    setItems(page.items);
    setPreferences(prefs);
    setError(null);
  }, [scope.organizationId, unreadOnly]);

  useEffect(() => {
    if (!scope.organizationId) return;
    const controller = new AbortController();
    queueMicrotask(() => { if (!controller.signal.aborted) void load(controller.signal).catch(() => setError("Notifications could not be loaded.")); });
    return () => controller.abort();
  }, [load, scope.organizationId]);

  async function create() {
    if (!scope.organizationId || !title.trim() || !body.trim()) return;
    setBusy(true); setError(null);
    try {
      await createNotification(scope.organizationId, { branchId: scope.branchId || undefined, title: title.trim(), body: body.trim(), severity });
      setTitle(""); setBody(""); await load();
    } catch { setError("Notification could not be created."); } finally { setBusy(false); }
  }
  async function markRead(item: NotificationItem) {
    if (!scope.organizationId || item.isRead) return;
    setBusy(true); setError(null);
    try { await markNotificationRead(scope.organizationId, item.id); await load(); }
    catch { setError("Notification could not be marked as read."); }
    finally { setBusy(false); }
  }

  async function savePreferences(next: NotificationPreferences) {
    if (!scope.organizationId) return;
    setBusy(true); setError(null);
    try { setPreferences(await updateNotificationPreferences(scope.organizationId, next)); }
    catch { setError("Notification preferences could not be saved."); }
    finally { setBusy(false); }
  }

  return <section className="manager-content">
    <div className="manager-title"><div><span className="eyebrow">NOTIFICATIONS</span><h1>Notification center</h1><p>Review operational alerts and delivery preferences.</p></div><div className="metric-card"><strong>{items.filter(x => !x.isRead).length}</strong><span>Unread loaded</span></div></div>
    <OperationsScopeSelector scope={scope}/>{error && <p className="error-banner">{error}</p>}
    <div className="split-grid"><div className="panel"><h2>Inbox</h2>
      <button disabled={busy} onClick={() => setUnreadOnly(value => !value)}>{unreadOnly ? "Show all" : "Unread only"}</button>
      <div className="data-list">{items.map(item => <article key={item.id}><strong>{item.title}</strong><span>{item.severity} · {new Date(item.createdAt).toLocaleString()}</span><p>{item.body}</p>{!item.isRead && <button disabled={busy} onClick={() => void markRead(item)}>Mark read</button>}</article>)}</div>
    </div>
    <div className="panel"><h2>Preferences</h2>{preferences && <>
      <label><input type="checkbox" checked={preferences.inAppEnabled} onChange={event => void savePreferences({ ...preferences, inAppEnabled: event.target.checked })}/> In-app</label>
      <label><input type="checkbox" checked={preferences.emailEnabled} onChange={event => void savePreferences({ ...preferences, emailEnabled: event.target.checked })}/> Email</label>
      <label><input type="checkbox" checked={preferences.pushEnabled} onChange={event => void savePreferences({ ...preferences, pushEnabled: event.target.checked })}/> Push</label>
    </>}
    <h2>Self-test alert</h2>
      <label>Severity<select value={severity} onChange={event => setSeverity(event.target.value as typeof severity)}><option value="info">Info</option><option value="warning">Warning</option><option value="critical">Critical</option></select></label>
      <label>Title<input maxLength={160} value={title} onChange={event => setTitle(event.target.value)}/></label>
      <label>Body<textarea maxLength={2000} value={body} onChange={event => setBody(event.target.value)}/></label>
      <button disabled={busy || !scope.organizationId || !title.trim() || !body.trim()} onClick={() => void create()}>Create self-test alert</button>
    </div></div>
  </section>;
}
