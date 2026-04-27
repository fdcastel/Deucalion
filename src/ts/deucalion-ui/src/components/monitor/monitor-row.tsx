import { type Component, createEffect, createMemo, createSignal, onCleanup, Show } from "solid-js";

import { MonitorState, type MonitorProps } from "../../services/deucalion-types";
import { avail, lastIncident } from "../../services/monitor-stats";
import { fmtAgo, stateName } from "../../services/formatting";

import { HeartbeatStrip } from "./heartbeat-strip";
import { LatStats } from "./lat-stats";
import { Sparkline } from "./sparkline";

interface MonitorRowProps {
  monitor: MonitorProps;
}

export const MonitorRow: Component<MonitorRowProps> = (props) => {
  const lastState = (): MonitorState => {
    const fromStats = props.monitor.stats?.lastState;
    if (fromStats !== undefined) return fromStats;
    return props.monitor.events[0]?.st ?? MonitorState.Unknown;
  };

  const availability = (): number => props.monitor.stats?.availability ?? avail(props.monitor.events);

  const sparkValues = createMemo(() => {
    const evs = props.monitor.events;
    const out: number[] = [];
    for (let i = evs.length - 1; i >= 0; i--) {
      const ms = evs[i].ms;
      if (ms != null) out.push(ms);
    }
    return out;
  });

  const incident = createMemo(() => lastIncident(props.monitor.events));

  // Flash on update — track top event timestamp, set .flash for ~500ms.
  const [flash, setFlash] = createSignal(false);
  let lastSeenAt: number | undefined;
  createEffect(() => {
    if (props.monitor.events.length === 0) return;
    const top = props.monitor.events[0].at;
    if (top === lastSeenAt) return;
    const isFirstSeen = lastSeenAt === undefined;
    lastSeenAt = top;
    if (isFirstSeen) return;
    setFlash(true);
    const id = setTimeout(() => { setFlash(false); }, 600);
    onCleanup(() => { clearTimeout(id); });
  });

  const rowClass = (): string => {
    const parts = ["row"];
    const s = lastState();
    if (s === MonitorState.Down) parts.push("is-down");
    else if (s === MonitorState.Warn) parts.push("is-warn");
    else if (s === MonitorState.Degraded) parts.push("is-degraded");
    if (flash()) parts.push("flash");
    return parts.join(" ");
  };

  const availClass = (): string => {
    const a = availability();
    if (a < 90) return "avail down";
    if (a < 99) return "avail warn";
    return "avail";
  };

  const typeClass = (): string => `type-badge t-${props.monitor.config.type}`;

  const [statsOpen, setStatsOpen] = createSignal(false);

  return (
    <div class={rowClass()}>
      <div class="col-name">
        <span class={typeClass()}>{props.monitor.config.type}</span>
        <span class="row-name" title={props.monitor.name}>
          <Show when={props.monitor.config.href} fallback={props.monitor.name}>
            <a href={props.monitor.config.href} target="_blank" rel="noopener noreferrer">{props.monitor.name}</a>
          </Show>
        </span>
      </div>
      <HeartbeatStrip events={props.monitor.events} />
      <button
        type="button"
        class={`col-stats${statsOpen() ? " is-open" : ""}`}
        aria-label="Toggle latency percentiles"
        aria-expanded={statsOpen()}
        onClick={() => { setStatsOpen((v) => !v); }}
      >
        <Sparkline values={sparkValues()} max={props.monitor.stats?.warnTimeoutMs} />
        <div class="lat-stats-pop" role="tooltip">
          <LatStats monitor={props.monitor} />
        </div>
      </button>
      <div class="col-right">
        <span class={availClass()}>{availability().toFixed(2)}%</span>
        <Show
          when={incident()}
          fallback={<span class="last-incident">no incident</span>}
        >
          {(inc) => (
            <span class="last-incident">
              <span class="last-incident-ago">{fmtAgo(inc().end)}</span>
              <span class="last-incident-sep"> · </span>
              <span class="last-incident-state">{stateName(inc().state)}</span>
            </span>
          )}
        </Show>
      </div>
    </div>
  );
};
