import { For, type Component, createEffect, createMemo, createSignal, onCleanup } from "solid-js";
import { stripLen as sharedStripLen } from "../../services/viewport";

import type { MonitorEventDto } from "../../services/deucalion-types";
import { fmtMs, fmtTime, stateLabel, stateName } from "../../services/formatting";

// Strip length by viewport tier and the shared resize signal live in
// services/viewport.ts: the monitors store asks the API for exactly that many
// events, so both sides must agree on the tiers.
const useStripLen = sharedStripLen;

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
