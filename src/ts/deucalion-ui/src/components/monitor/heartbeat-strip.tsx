import { For, type Accessor, type Component, createEffect, createMemo, createSignal, onCleanup } from "solid-js";

import type { MonitorEventDto } from "../../services/deucalion-types";
import { fmtMs, fmtTime, stateLabel, stateName } from "../../services/formatting";

// Viewport-tier'd strip lengths, picked from a Playwright sweep that
// measured the col-strip width and per-tick width at each breakpoint.
// Targets keep tick width ≥ ~3px (visibly distinguishable colour bands)
// rather than crushing every tick to the 2px CSS minimum:
//   ≥1480 → 120 ticks (~4.8px each on a 812px strip)
//   ≥1280 →  90 ticks (~4.8px each on a ~612px strip)
//    ≥720 →  60 ticks (~4–5px each on a 360–500px strip)
//   < 720 →  60 ticks (mobile — already crushed to 2px, more wouldn't help)
const stripLenForWidth = (w: number): number => {
  if (w >= 1480) return 120;
  if (w >= 1280) return 90;
  return 60;
};

// One signal + one `resize` listener shared by every strip on the page.
// Attached lazily on first use so importing this module has no side effects
// (tests pin `window.innerWidth` before rendering, and SSR has no window).
let sharedStripLen: Accessor<number> | undefined;

const useStripLen = (): Accessor<number> => {
  if (sharedStripLen) return sharedStripLen;
  if (typeof window === "undefined") return () => 60;
  const [len, setLen] = createSignal(stripLenForWidth(window.innerWidth));
  window.addEventListener("resize", () => { setLen(stripLenForWidth(window.innerWidth)); });
  sharedStripLen = len;
  return len;
};

interface HeartbeatStripProps {
  events: MonitorEventDto[]; // newest-first
}

const tipFor = (ev: MonitorEventDto): string =>
  `${stateLabel(ev.st)} · ${fmtTime(ev.at)}${ev.ms != null ? ` · ${fmtMs(ev.ms)}` : ""}`;

export const HeartbeatStrip: Component<HeartbeatStripProps> = (props) => {
  const [fresh, setFresh] = createSignal(false);
  let lastSeenAt: number | undefined;
  const stripLen = useStripLen();

  // When the freshest event timestamp changes, flag the strip as fresh for
  // ~600ms. CSS (`.col-strip.is-fresh .tick:last-child`) animates the newest
  // tick, so this is a single binding on the container rather than one
  // per tick.
  createEffect(() => {
    if (props.events.length === 0) return;
    const top = props.events[0].at;
    if (top === lastSeenAt) return;
    lastSeenAt = top;
    setFresh(true);
    const id = setTimeout(() => { setFresh(false); }, 600);
    onCleanup(() => { clearTimeout(id); });
  });

  const oldestToNewest = createMemo(() => {
    // Backend returns up to 120 events newest-first; we render at most
    // `stripLen()` (viewport-dependent) oldest-on-the-left,
    // freshest-on-the-right, padded with null on the left if fewer.
    const arr: (MonitorEventDto | null)[] = [];
    const evs = props.events;
    const len = stripLen();
    const n = Math.min(len, evs.length);
    for (let i = 0; i < len - n; i++) arr.push(null);
    for (let i = n - 1; i >= 0; i--) arr.push(evs[i]);
    return arr;
  });

  // Accessible name for the whole strip. The per-tick tooltips are CSS-only
  // (hover), so the newest check's detail is surfaced here for screen
  // readers, and the strip is focusable so keyboard users can reveal the
  // same tooltip (see `.col-strip:focus-visible` in layout.css).
  const label = createMemo((): string => {
    const evs = props.events;
    if (evs.length === 0) return "Recent check history: no checks yet";
    const shown = Math.min(stripLen(), evs.length);
    return `Recent check history: ${shown.toString()} checks, latest ${tipFor(evs[0])}`;
  });

  return (
    <div
      class="col-strip"
      classList={{ "is-fresh": fresh() }}
      role="img"
      tabindex="0"
      aria-label={label()}
    >
      <For each={oldestToNewest()}>
        {(ev) => {
          if (ev === null) return <span class="tick unknown" />;
          return <span class={`tick ${stateName(ev.st)}`} data-tip={tipFor(ev)} />;
        }}
      </For>
    </div>
  );
};
