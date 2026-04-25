import { For, Show, type Component, createSignal, onCleanup, onMount } from "solid-js";

import { tweaks } from "../../stores/tweaks-store";
import { DISPLAY_FONTS, MONO_FONTS, UI_FONTS } from "./fonts";
import { TweakRadio, TweakSection, TweakSelect, TweakSlider } from "./tweaks-controls";
import { XIcon } from "../common/icons";

const PAD = 16;

interface AccentPreset { name: string; h: number; c: number }
const ACCENT_PRESETS: AccentPreset[] = [
  { name: "Teal",    h: 178, c: 0.12 },
  { name: "Cyan",    h: 220, c: 0.12 },
  { name: "Indigo",  h: 265, c: 0.13 },
  { name: "Magenta", h: 330, c: 0.14 },
  { name: "Amber",   h: 70,  c: 0.14 },
  { name: "Lime",    h: 130, c: 0.13 },
];

interface FontPreset { label: string; display: string; ui: string; mono: string }
const FONT_PRESETS: FontPreset[] = [
  { label: "Editorial", display: "newsreader",   ui: "inter",        mono: "jetbrains" },
  { label: "Terminal",  display: "jetbrains",    ui: "ibmsans",      mono: "jetbrains" },
  { label: "Modern",    display: "spacegrotesk", ui: "spacegrotesk", mono: "spacemono" },
  { label: "IBM Plex",  display: "ibmsans",      ui: "ibmsans",      mono: "ibmmono" },
];

const fontOptions = (catalog: Record<string, { label: string }>): { value: string; label: string }[] =>
  Object.entries(catalog).map(([value, def]) => ({ value, label: def.label }));

// The panel body is its own component so that onMount + onCleanup
// (and the ResizeObserver inside them) only run while the panel is
// actually rendered. Putting the lifecycle in the parent caused
// `panelRef.offsetWidth` to fire against an unbound ref on initial
// load when the panel was closed.
const PanelBody: Component = () => {
  let panelRef: HTMLDivElement | undefined;
  const [pos, setPos] = createSignal({ x: PAD, y: PAD });

  const clampToViewport = (): void => {
    if (!panelRef) return;
    const w = panelRef.offsetWidth, h = panelRef.offsetHeight;
    const maxRight = Math.max(PAD, window.innerWidth - w - PAD);
    const maxBottom = Math.max(PAD, window.innerHeight - h - PAD);
    setPos((p) => ({
      x: Math.min(maxRight, Math.max(PAD, p.x)),
      y: Math.min(maxBottom, Math.max(PAD, p.y)),
    }));
  };

  onMount(() => {
    clampToViewport();
    if (typeof ResizeObserver === "undefined") {
      window.addEventListener("resize", clampToViewport);
      onCleanup(() => { window.removeEventListener("resize", clampToViewport); });
      return;
    }
    const ro = new ResizeObserver(clampToViewport);
    ro.observe(document.documentElement);
    onCleanup(() => { ro.disconnect(); });
  });

  const onDragStart = (e: MouseEvent): void => {
    if (!panelRef) return;
    const r = panelRef.getBoundingClientRect();
    const sx = e.clientX, sy = e.clientY;
    const startRight = window.innerWidth - r.right;
    const startBottom = window.innerHeight - r.bottom;
    const move = (ev: MouseEvent): void => {
      setPos({
        x: startRight - (ev.clientX - sx),
        y: startBottom - (ev.clientY - sy),
      });
      clampToViewport();
    };
    const up = (): void => {
      window.removeEventListener("mousemove", move);
      window.removeEventListener("mouseup", up);
    };
    window.addEventListener("mousemove", move);
    window.addEventListener("mouseup", up);
  };

  return (
    <div
      ref={(el) => { panelRef = el; }}
      class="twk-panel"
      data-testid="tweaks-panel"
      style={{ right: `${pos().x.toString()}px`, bottom: `${pos().y.toString()}px` }}
    >
      <div class="twk-hd" onMouseDown={onDragStart}>
        <b>Tweaks</b>
        <button
          class="twk-x"
          aria-label="Close tweaks"
          onMouseDown={(e) => { e.stopPropagation(); }}
          onClick={() => { tweaks.setPanelOpen(false); }}
        >
          <XIcon size={12} />
        </button>
      </div>
      <div class="twk-body">
        <TweakSection label="Theme">
          <TweakRadio
            label="Mode"
            value={tweaks.theme()}
            options={[{ value: "dark", label: "Dark" }, { value: "light", label: "Light" }]}
            onChange={(v) => { tweaks.setTheme(v as "dark" | "light"); }}
          />
        </TweakSection>
        <TweakSection label="Accent">
          <TweakSlider
            label="Hue" min={0} max={360} step={1}
            unit="°"
            value={tweaks.accentHue()}
            onChange={(v) => { tweaks.setAccentHue(v); }}
          />
          <TweakSlider
            label="Saturation" min={0} max={0.25} step={0.005}
            value={tweaks.accentChroma()}
            onChange={(v) => { tweaks.setAccentChroma(v); }}
          />
          <div style={{ display: "flex", gap: "6px", "margin-top": "6px", "flex-wrap": "wrap" }}>
            <For each={ACCENT_PRESETS}>
              {(p) => (
                <button
                  class="twk-swatch"
                  title={p.name}
                  style={{ background: `oklch(0.7 ${p.c.toString()} ${p.h.toString()})` }}
                  onClick={() => {
                    tweaks.setAccentHue(p.h);
                    tweaks.setAccentChroma(p.c);
                  }}
                />
              )}
            </For>
          </div>
        </TweakSection>
        <TweakSection label="Typography">
          <TweakSelect
            label="Headlines"
            value={tweaks.displayFont()}
            options={fontOptions(DISPLAY_FONTS)}
            onChange={(v) => { tweaks.setDisplayFont(v); }}
          />
          <TweakSelect
            label="UI / body"
            value={tweaks.uiFont()}
            options={fontOptions(UI_FONTS)}
            onChange={(v) => { tweaks.setUiFont(v); }}
          />
          <TweakSelect
            label="Monospace"
            value={tweaks.monoFont()}
            options={fontOptions(MONO_FONTS)}
            onChange={(v) => { tweaks.setMonoFont(v); }}
          />
          <div style={{ display: "flex", gap: "6px", "margin-top": "6px", "flex-wrap": "wrap" }}>
            <For each={FONT_PRESETS}>
              {(p) => (
                <button
                  class="twk-btn secondary"
                  onClick={() => {
                    tweaks.setDisplayFont(p.display);
                    tweaks.setUiFont(p.ui);
                    tweaks.setMonoFont(p.mono);
                  }}
                >
                  {p.label}
                </button>
              )}
            </For>
          </div>
        </TweakSection>
      </div>
    </div>
  );
};

export const TweaksPanel: Component = () => (
  <Show when={tweaks.panelOpen()}>
    <PanelBody />
  </Show>
);
