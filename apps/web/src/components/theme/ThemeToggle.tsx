"use client";

import { useEffect, useState } from "react";
import { oppositeTheme, resolveTheme, type Theme } from "./theme";

const storageKey = "salekhpos-theme";

function browserTheme(): Theme {
  return resolveTheme(
    window.localStorage.getItem(storageKey),
    window.matchMedia("(prefers-color-scheme: dark)").matches
  );
}

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme | null>(null);

  useEffect(() => {
    setTheme(browserTheme());
  }, []);

  function toggle() {
    const current = theme ?? browserTheme();
    const next = oppositeTheme(current);
    document.documentElement.dataset.theme = next;
    window.localStorage.setItem(storageKey, next);
    setTheme(next);
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
