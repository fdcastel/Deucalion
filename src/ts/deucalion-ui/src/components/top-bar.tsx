import { createEffect, type Component } from "solid-js";

import { configuration } from "../stores/configuration-store";
import { monitorList } from "../stores/monitors-store";
import { sseStatus } from "../stores/sse";
import { tweaks } from "../stores/tweaks-store";
import { MonitorState } from "../services/deucalion-types";

import { MoonIcon, SlidersIcon, SunIcon } from "./common/icons";

export const TopBar: Component = () => {
  const pageTitle = (): string => configuration()?.pageTitle ?? "Deucalion";
  const monitorCount = (): number => monitorList().length;

  const downCount = (): number => {
    let n = 0;
    for (const m of monitorList()) {
      const s = m.stats?.lastState ?? m.events[0]?.st;
      if (s === MonitorState.Down) n++;
    }
    return n;
  };

  // Preserve "(-N) Title" page-title pattern
  createEffect(() => {
    const t = pageTitle();
    const d = downCount();
    document.title = d > 0 ? `(-${d.toString()}) ${t}` : t;
  });

  const splitTitle = (): { head: string; tail: string } => {
    const t = pageTitle();
    const idx = t.lastIndexOf(" ");
    if (idx < 0) return { head: t, tail: "" };
    return { head: t.slice(0, idx), tail: t.slice(idx + 1) };
  };

  const connectionLabel = (): string => {
    switch (sseStatus()) {
      case "open": return "connected";
      case "connecting": return "connecting…";
      case "error": return "disconnected";
    }
  };

  return (
    <header class="topbar">
      <div class="brand">
        <span class="brand-mark" aria-hidden="true" />
        <div class="brand-text">
          <h1 class="brand-name">
            {splitTitle().head} {splitTitle().tail && <em>{splitTitle().tail}</em>}
          </h1>
          <div class="brand-meta">
            <span>Production</span>
            <span class="brand-sep" />
            <span>{monitorCount()} monitors</span>
          </div>
        </div>
      </div>
      <div class="topbar-right">
        <span class={`connection-dot ${sseStatus() === "open" ? "" : sseStatus()}`} />
        <span class="mono">{connectionLabel()}</span>
        <button
          class="theme-btn"
          aria-label="Toggle theme"
          onClick={() => { tweaks.setTheme(tweaks.theme() === "dark" ? "light" : "dark"); }}
        >
          {tweaks.theme() === "dark" ? <MoonIcon /> : <SunIcon />}
        </button>
        <button
          class="twk-trigger"
          aria-label="Open tweaks panel"
          data-tip="Tweaks"
          onClick={() => { tweaks.setPanelOpen(!tweaks.panelOpen()); }}
        >
          <SlidersIcon />
        </button>
      </div>
    </header>
  );
};
