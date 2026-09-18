"use client";

import { useSyncExternalStore } from "react";
import { normalizeTheme, oppositeTheme, resolveTheme, type Theme } from "./theme";

const storageKey = "salekhpos-theme";
const changeEvent = "salekhpos-theme-change";

function browserTheme(): Theme {
  return resolveTheme(
    window.localStorage.getItem(storageKey),
    window.matchMedia("(prefers-color-scheme: dark)").matches
  );
}

function snapshot(): Theme {
  return normalizeTheme(document.documentElement.dataset.theme ?? null) ?? browserTheme();
}

function subscribe(onStoreChange: () => void) {
  const media = window.matchMedia("(prefers-color-scheme: dark)");
  const handleThemeChange = () => onStoreChange();
  const handleSystemChange = () => {
    if (normalizeTheme(window.localStorage.getItem(storageKey)) !== null) return;
    document.documentElement.dataset.theme = media.matches ? "dark" : "light";
    onStoreChange();
  };

  window.addEventListener(changeEvent, handleThemeChange);
  media.addEventListener("change", handleSystemChange);
  return () => {
    window.removeEventListener(changeEvent, handleThemeChange);
    media.removeEventListener("change", handleSystemChange);
  };
}

export function ThemeToggle() {
  const theme = useSyncExternalStore<Theme>(subscribe, snapshot, () => "light");

  function toggle() {
    const next = oppositeTheme(theme);
    document.documentElement.dataset.theme = next;
    window.localStorage.setItem(storageKey, next);
    window.dispatchEvent(new Event(changeEvent));
  }

  const dark = theme === "dark";
  return <button
    type="button"
    className="theme-toggle"
    aria-label={dark ? "Switch to light theme" : "Switch to dark theme"}
    aria-pressed={dark}
    onClick={toggle}
  >
    <span className="theme-toggle-icon" aria-hidden="true">{dark ? "☀" : "◐"}</span>
    <span className="theme-toggle-label">{dark ? "Light mode" : "Dark mode"}</span>
  </button>;
}
