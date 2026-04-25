import { For, type Component, createEffect, createMemo, createSignal, onCleanup } from "solid-js";

import type { MonitorEventDto } from "../../services/deucalion-types";
import { fmtMs, fmtTime, stateLabel, stateName } from "../../services/formatting";

const STRIP_LEN = 60;

interface HeartbeatStripProps {
  events: MonitorEventDto[]; // newest-first
}

export const HeartbeatStrip: Component<HeartbeatStripProps> = (props) => {
  const [freshSig, setFreshSig] = createSignal(0);
  let lastSeenAt: number | undefined;

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
    // Backend returns up to 60 events newest-first; we want the strip
    // rendered oldest-on-the-left, freshest-on-the-right, padded with
    // null on the left if fewer than STRIP_LEN events.
    const arr: (MonitorEventDto | null)[] = [];
    const evs = props.events;
    const n = Math.min(STRIP_LEN, evs.length);
    for (let i = 0; i < STRIP_LEN - n; i++) arr.push(null);
    for (let i = n - 1; i >= 0; i--) arr.push(evs[i]);
    return arr;
  });

  return (
    <div class="col-strip" role="img" aria-label="Recent check history">
      <For each={oldestToNewest()}>
        {(ev, i) => {
          const fresh = (): boolean => i() === STRIP_LEN - 1 && freshSig() !== 0;
          if (ev === null) return <span class="tick unknown" />;
          const tip = `${stateLabel(ev.st)} · ${fmtTime(ev.at)}${ev.ms != null ? ` · ${fmtMs(ev.ms)}` : ""}`;
          return <span class={`tick ${stateName(ev.st)}${fresh() ? " fresh" : ""}`} data-tip={tip} />;
        }}
      </For>
    </div>
  );
};
