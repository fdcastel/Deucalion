import { createEffect, Show, type Component } from "solid-js";

import { configuration } from "../stores/configuration-store";
import { monitorList } from "../stores/monitors-store";
import { sseStatus } from "../stores/sse";
import { tweaks } from "../stores/tweaks-store";
import { MonitorState } from "../services/deucalion-types";

import { MoonIcon, SlidersIcon, SunIcon } from "./common/icons";

// Strip any *emphasis* markers so the "(-N) Title" effect is plain text.
const cleanTitle = (t: string): string => t.replace(/\*([^*]+)\*/g, "$1");

// `pageTitle` may carry an inline `*emphasis*` marker (e.g. "Araponga *status*").
// The marker is what produces the prototype's italic accent fragment; backends
// that don't opt in get a flat title with no italic guesswork.
const parseTitle = (t: string): { head: string; emphasis: string; tail: string } => {
  const m = /^(.*?)\*([^*]+)\*(.*)$/.exec(t);
  if (!m) return { head: t, emphasis: "", tail: "" };
  return { head: m[1], emphasis: m[2], tail: m[3] };
};

export const TopBar: Component = () => {
  const pageTitle = (): string => configuration()?.pageTitle ?? "Deucalion";

  const downCount = (): number => {
    let n = 0;
    for (const m of monitorList()) {
      const s = m.stats?.lastState ?? m.events[0]?.st;
      if (s === MonitorState.Down) n++;
    }
    return n;
  };

  // Preserve "(-N) Title" page-title pattern (without the asterisk markers).
  createEffect(() => {
    const t = cleanTitle(pageTitle());
    const d = downCount();
    document.title = d > 0 ? `(-${d.toString()}) ${t}` : t;
  });

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
        <h1 class="brand-name">
          {parseTitle(pageTitle()).head}
          <Show when={parseTitle(pageTitle()).emphasis}>
            <em>{parseTitle(pageTitle()).emphasis}</em>
          </Show>
          {parseTitle(pageTitle()).tail}
        </h1>
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
