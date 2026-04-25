import { createEffect, createSignal } from "solid-js";

import { DISPLAY_FONTS, MONO_FONTS, UI_FONTS } from "../components/tweaks/fonts";

export type ThemeMode = "dark" | "light";

export interface TweaksState {
  theme: ThemeMode;
  accentHue: number;
  accentChroma: number;
  displayFont: string;
  uiFont: string;
  monoFont: string;
}

const STORAGE_KEY = "deucalion.tweaks";
const LEGACY_THEME_KEY = "theme";

const DEFAULTS: TweaksState = {
  theme: "dark",
  accentHue: 178,
  accentChroma: 0.12,
  // IBM Plex preset — matches the "IBM Plex" font preset shown in the
  // tweaks panel.
  displayFont: "ibmsans",
  uiFont: "ibmsans",
  monoFont: "ibmmono",
};

const loadFromStorage = (): TweaksState => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<TweaksState>;
      return { ...DEFAULTS, ...parsed };
    }
    const legacy = localStorage.getItem(LEGACY_THEME_KEY);
    if (legacy === "dark" || legacy === "light") {
      localStorage.removeItem(LEGACY_THEME_KEY);
      return { ...DEFAULTS, theme: legacy };
    }
  } catch {
    /* ignore — fall through to system defaults */
  }
  if (typeof window !== "undefined" && window.matchMedia("(prefers-color-scheme: light)").matches) {
    return { ...DEFAULTS, theme: "light" };
  }
  return { ...DEFAULTS };
};

const initial = loadFromStorage();

const [theme, setTheme] = createSignal<ThemeMode>(initial.theme);
const [accentHue, setAccentHue] = createSignal<number>(initial.accentHue);
const [accentChroma, setAccentChroma] = createSignal<number>(initial.accentChroma);
const [displayFont, setDisplayFont] = createSignal<string>(initial.displayFont);
const [uiFont, setUiFont] = createSignal<string>(initial.uiFont);
const [monoFont, setMonoFont] = createSignal<string>(initial.monoFont);
const [panelOpen, setPanelOpen] = createSignal<boolean>(false);

export const tweaks = {
  theme, setTheme,
  accentHue, setAccentHue,
  accentChroma, setAccentChroma,
  displayFont, setDisplayFont,
  uiFont, setUiFont,
  monoFont, setMonoFont,
  panelOpen, setPanelOpen,
};

const persist = (): void => {
  try {
    const snap: TweaksState = {
      theme: theme(),
      accentHue: accentHue(),
      accentChroma: accentChroma(),
      displayFont: displayFont(),
      uiFont: uiFont(),
      monoFont: monoFont(),
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(snap));
  } catch {
    /* localStorage unavailable — silent */
  }
};

const applyThemeAndAccent = (): void => {
  if (typeof document === "undefined") return;
  const root = document.documentElement;
  const t = theme();
  const h = accentHue();
  const c = accentChroma();
  root.setAttribute("data-theme", t);
  root.style.setProperty("--accent-h", h.toString());
  root.style.setProperty("--accent-c", c.toString());
  // Set derived accent vars imperatively (oklch() with calc() isn't always
  // re-resolved when the input vars change in some browsers).
  const isLight = t === "light";
  root.style.setProperty("--accent",      `oklch(${isLight ? "0.55" : "0.78"} ${c.toString()} ${h.toString()})`);
  root.style.setProperty("--accent-dim",  `oklch(${isLight ? "0.7" : "0.62"} ${(c * 0.85).toString()} ${h.toString()})`);
  root.style.setProperty("--accent-soft", `oklch(${isLight ? "0.85" : "0.32"} ${(c * 0.6).toString()} ${h.toString()} / ${isLight ? "0.3" : "0.35"})`);
  root.style.setProperty("--flash",       `oklch(${isLight ? "0.7" : "0.55"} ${Math.max(c, 0.08).toString()} ${h.toString()} / 0.18)`);
};

const applyFonts = (): void => {
  if (typeof document === "undefined") return;
  const disp = DISPLAY_FONTS[displayFont()] ?? DISPLAY_FONTS.newsreader;
  const ui = UI_FONTS[uiFont()] ?? UI_FONTS.inter;
  const mono = MONO_FONTS[monoFont()] ?? MONO_FONTS.jetbrains;
  const italic = disp.italicize ? "italic" : "normal";
  let tag = document.getElementById("dynamic-fonts");
  if (!tag) {
    tag = document.createElement("style");
    tag.id = "dynamic-fonts";
    document.head.appendChild(tag);
  }
  tag.textContent = `
    html, body, .ui-font { font-family: ${ui.stack} !important; }
    .mono, .feed-time, .feed-name, .feed-arrow, .feed-state, .feed-detail,
    .group-meta, .row-name, .type-badge, .lat-stats, .avail, .last-incident,
    .subgroup, .footer, [data-tip]:hover::after, .hero-chip strong,
    .hero-meta em, .feed-live, .tnum, .col-stats .lat-stats span strong
      { font-family: ${mono.stack} !important; }
    .serif, .brand-name, .hero-availability, .group-title, .footer em
      { font-family: ${disp.stack} !important; }
    .brand-name em, .group-title em, .footer em, .hero-availability .pct
      { font-style: ${italic} !important; }
    :root { --display-italic: ${italic}; }
  `;
};

createEffect(() => {
  applyThemeAndAccent();
  persist();
});

createEffect(() => {
  applyFonts();
  persist();
});

// Easter-egg trigger: the visible "Open tweaks panel" button is gone, so
// power users summon the panel from the JS console via window.deucalion().
if (typeof window !== "undefined") {
  (window as unknown as { deucalion?: () => void }).deucalion = (): void => {
    setPanelOpen(true);
  };
}

// Test-only: reset signals to defaults and clear the storage key.
export const __resetTweaksForTests = (): void => {
  setTheme(DEFAULTS.theme);
  setAccentHue(DEFAULTS.accentHue);
  setAccentChroma(DEFAULTS.accentChroma);
  setDisplayFont(DEFAULTS.displayFont);
  setUiFont(DEFAULTS.uiFont);
  setMonoFont(DEFAULTS.monoFont);
  setPanelOpen(false);
  try { localStorage.removeItem(STORAGE_KEY); } catch { /* ignore */ }
};
