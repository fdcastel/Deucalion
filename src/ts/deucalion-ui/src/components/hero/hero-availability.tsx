import { type Component, createMemo } from "solid-js";

import { monitorList } from "../../stores/monitors-store";
import { aggregateAvailability } from "../../services/monitor-stats";
import { Sparkline } from "../monitor/sparkline";

export const HeroAvailability: Component = () => {
  const agg = createMemo(() => aggregateAvailability(monitorList()));

  const trend = createMemo(() => {
    // Build a 60-bucket trend by averaging the i-th most-recent event across
    // all monitors. Events are newest-first, so we walk index 0..59 and the
    // resulting array is also newest-first; reverse it so the sparkline draws
    // oldest -> newest left-to-right.
    const monitors = monitorList();
    if (monitors.length === 0) return [];
    const buckets: number[] = [];
    for (let i = 0; i < 60; i++) {
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
      <div>
        <div class="hero-label">Availability · 60 ticks</div>
        <div class="hero-stat-grid" style={{ "margin-top": "10px" }}>
          <div>
            <div class="hero-availability">
              <span>{fmtPct(agg().weightedAvailability).whole}</span>
              <span class="pct">.{fmtPct(agg().weightedAvailability).dec}%</span>
            </div>
            <div class="hero-summary-row">
              <span class="hero-chip up"><strong>{agg().states.up.toString()}</strong> online</span>
              {agg().states.warn > 0 && <span class="hero-chip warn"><strong>{agg().states.warn.toString()}</strong> warn</span>}
              {agg().states.degraded > 0 && <span class="hero-chip warn"><strong>{agg().states.degraded.toString()}</strong> degraded</span>}
              {agg().states.down > 0 && <span class="hero-chip down"><strong>{agg().states.down.toString()}</strong> down</span>}
              <span class="hero-chip hero-chip-total">of <strong>{agg().total.toString()}</strong></span>
            </div>
          </div>
          <div class="hero-spark-wrap">
            <div class="hero-spark-meta">trend <em>response</em></div>
            <div style={{ width: "100%", height: "56px" }}>
              <Sparkline values={trend()} height={56} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
