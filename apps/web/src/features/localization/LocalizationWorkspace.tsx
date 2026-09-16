"use client";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { getLocalizationSettings, updateLocalizationSettings } from "./api";
import type { LocalizationSettings } from "./types";

export function LocalizationWorkspace() {
  const scope = useOperationsScope();
  const [settings, setSettings] = useState<LocalizationSettings | null>(null);
  const [countryCode, setCountryCode] = useState("");
  const [defaultLocale, setDefaultLocale] = useState("");
  const [currency, setCurrency] = useState("");
  const [timeZone, setTimeZone] = useState("");
  const [locales, setLocales] = useState("");
  const [firstDay, setFirstDay] = useState("1");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const apply = useCallback((value: LocalizationSettings | null) => {
    setSettings(value);
    setCountryCode(value?.countryCode ?? "");
    setDefaultLocale(value?.defaultLocale ?? "");
    setCurrency(value?.defaultCurrency ?? "");
    setTimeZone(value?.timeZone ?? "");
    setLocales(value?.supportedLocales.join(", ") ?? "");
    setFirstDay(String(value?.firstDayOfWeek ?? 1));
  }, []);
  const load = useCallback(async () => {
    if (!scope.organizationId) return;
    setBusy(true); setMessage(null);
    try { apply(await getLocalizationSettings(scope.organizationId)); }
    catch { setMessage("Localization settings could not be loaded."); }
    finally { setBusy(false); }
  }, [apply, scope.organizationId]);

  useEffect(() => {
    if (!scope.organizationId) return;
    const id = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(id);
  }, [load, scope.organizationId]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!scope.organizationId) return;
    const supportedLocales = locales.split(",").map(x => x.trim()).filter(Boolean);
    const day = Number(firstDay);
    setBusy(true); setMessage(null);
    try {
      const result = await updateLocalizationSettings(scope.organizationId, {
        countryCode: countryCode.trim().toUpperCase(), defaultLocale: defaultLocale.trim(),
        defaultCurrency: currency.trim().toUpperCase(), timeZone: timeZone.trim(),
        supportedLocales, firstDayOfWeek: day, expectedVersion: settings?.version ?? null,
      });
      apply(result.settings);
      setMessage(result.applied ? "Localization settings saved." : "The same operation was already applied.");
    } catch { setMessage("Localization settings could not be saved."); }
    finally { setBusy(false); }
  }
  return <section className="manager-content">
    <div className="manager-title"><div><span className="eyebrow">GLOBALIZATION</span>
      <h1>Localization</h1><p>Configure country, locale, currency and timezone defaults for this organization.</p>
    </div>{settings ? <div className="metric-card"><strong>v{settings.version}</strong><span>Current version</span></div> : null}</div>
    <OperationsScopeSelector scope={scope}/>{message ? <p className="error-banner">{message}</p> : null}
    <form className="panel" onSubmit={submit}>
      <div className="split-grid">
        <label>Country code<input maxLength={2} value={countryCode} onChange={e=>setCountryCode(e.target.value.toUpperCase())}/></label>
        <label>Default locale<input maxLength={35} value={defaultLocale} onChange={e=>setDefaultLocale(e.target.value)}/></label>
        <label>Default currency<input maxLength={3} value={currency} onChange={e=>setCurrency(e.target.value.toUpperCase())}/></label>
        <label>Timezone<input maxLength={100} value={timeZone} onChange={e=>setTimeZone(e.target.value)}/></label>
        <label>Supported locales<input value={locales} onChange={e=>setLocales(e.target.value)} placeholder="en, az, ka"/></label>
        <label>First day of week<input inputMode="numeric" value={firstDay} onChange={e=>setFirstDay(e.target.value)}/></label>
      </div>
      <button disabled={busy || !scope.organizationId}>{busy ? "Saving…" : "Save localization"}</button>
    </form>
  </section>;
}
