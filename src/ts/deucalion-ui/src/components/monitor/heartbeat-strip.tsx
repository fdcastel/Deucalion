import { For, type Component, createEffect, createMemo, createSignal, onCleanup, onMount } from "solid-js";

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

const useStripLen = (): (() => number) => {
  const [len, setLen] = createSignal(
    typeof window === "undefined" ? 60 : stripLenForWidth(window.innerWidth),
  );
  onMount(() => {
    const update = (): void => { setLen(stripLenForWidth(window.innerWidth)); };
    window.addEventListener("resize", update);
    onCleanup(() => { window.removeEventListener("resize", update); });
  });
  return len;
};

interface HeartbeatStripProps {
  events: MonitorEventDto[]; // newest-first
}

export const HeartbeatStrip: Component<HeartbeatStripProps> = (props) => {
  const [freshSig, setFreshSig] = createSignal(0);
  let lastSeenAt: number | undefined;
  const stripLen = useStripLen();

  // When the freshest event timestamp changes, set the fresh signal so the
  // rightmost tick gets the .fresh class for ~600ms.
  createEffect(() => {
    if (props.events.length === 0) return;
    const top = props.events[0].at;
    if (top === lastSeenAt) return;
    lastSeenAt = top;
    setFreshSig(Date.now());
    const id = setTimeout(() => { setFreshSig(0); }, 600);
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

  return (
    <div class="col-strip" role="img" aria-label="Recent check history">
      <For each={oldestToNewest()}>
        {(ev, i) => {
          const fresh = (): boolean => i() === stripLen() - 1 && freshSig() !== 0;
          if (ev === null) return <span class="tick unknown" />;
          const tip = `${stateLabel(ev.st)} · ${fmtTime(ev.at)}${ev.ms != null ? ` · ${fmtMs(ev.ms)}` : ""}`;
          return <span class={`tick ${stateName(ev.st)}${fresh() ? " fresh" : ""}`} data-tip={tip} />;
        }}
      </For>
    </div>
  );
};
