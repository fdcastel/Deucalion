import { type Component, createMemo } from "solid-js";

import { MAX_EVENT_HISTORY } from "../../configuration";
import { monitorList } from "../../stores/monitors-store";
import { aggregateAvailability } from "../../services/monitor-stats";
import { Sparkline } from "../monitor/sparkline";

export const HeroAvailability: Component = () => {
  const agg = createMemo(() => aggregateAvailability(monitorList()));

  const trend = createMemo(() => {
    // Average the i-th most-recent event across all monitors to build a
    // per-bucket trend. Events are newest-first; reverse so the sparkline
    // draws oldest -> newest left-to-right.
    const monitors = monitorList();
    if (monitors.length === 0) return [];
    const buckets: number[] = [];
    for (let i = 0; i < MAX_EVENT_HISTORY; i++) {
      let sum = 0;
      let n = 0;
      for (const m of monitors) {
        const ms = m.events[i]?.ms;
        if (ms != null) { sum += ms; n++; }
      }
      if (n > 0) buckets.push(sum / n);
    }
    return buckets.reverse();
  });

  const fmtPct = (n: number): { whole: string; dec: string } => {
    const fixed = n.toFixed(2);
    const [whole, dec] = fixed.split(".");
    return { whole: whole, dec };
  };

  return (
    <div class="hero-stat">
      <div class="hero-stat-grid">
        <div class="hero-meta hero-meta-left">availability</div>
        <div class="hero-meta hero-meta-right">trend <em>response</em></div>
        <div class="hero-content-left">
          <div class="hero-availability">
            <span>{fmtPct(agg().weightedAvailability).whole}</span>
            <span class="pct">.{fmtPct(agg().weightedAvailability).dec}%</span>
          </div>
          <div class="hero-summary-row">
            <span class="hero-chip up"><strong>{agg().states.up.toString()}</strong> online</span>
            {agg().states.warn > 0 && <span class="hero-chip warn"><strong>{agg().states.warn.toString()}</strong> warn</span>}
            {agg().states.degraded > 0 && <span class="hero-chip warn"><strong>{agg().states.degraded.toString()}</strong> degraded</span>}
            {agg().states.down > 0 && <span class="hero-chip down"><strong>{agg().states.down.toString()}</strong> down</span>}
          </div>
        </div>
        <div class="hero-spark-wrap">
          <Sparkline values={trend()} height={48} />
        </div>
      </div>
    </div>
  );
};
