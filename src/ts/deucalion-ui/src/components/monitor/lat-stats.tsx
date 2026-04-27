import type { Component } from "solid-js";

import type { MonitorProps } from "../../services/deucalion-types";
import { fmtMs } from "../../services/formatting";
import { minMs, percentile } from "../../services/monitor-stats";
import { SPARKLINE_NOISE_FLOOR_MS } from "./sparkline";

interface LatStatsProps {
  monitor: MonitorProps;
}

export const LatStats: Component<LatStatsProps> = (props) => {
  // Prefer backend-computed percentiles when present; fall back to a
  // client-side computation over the event window.
  const stats = (): MonitorProps["stats"] => props.monitor.stats;
  const min = (): number | undefined => stats()?.minResponseTimeMs ?? minMs(props.monitor.events);
  const p50 = (): number | undefined => stats()?.latency50Ms ?? percentile(props.monitor.events, 0.5);
  const p95 = (): number | undefined => stats()?.latency95Ms ?? percentile(props.monitor.events, 0.95);
  const p99 = (): number | undefined => stats()?.latency99Ms ?? percentile(props.monitor.events, 0.99);
  const warn = (): number | undefined => stats()?.warnTimeoutMs;
  const timeout = (): number | undefined => stats()?.timeoutMs;
  const chartMax = (): number | undefined => {
    const w = warn();
    return w === undefined ? undefined : Math.max(w, SPARKLINE_NOISE_FLOOR_MS);
  };

  // 3 percentile rows on the left, 4 range/threshold rows on the right.
  // WARN cells use --warn (matching the sparkline reference line); TIMEOUT
  // uses --down to signal the hard failure threshold. The fourth right row
  // is placed via .col-right-start so the bottom-left corner stays empty.
  return (
    <div class="lat-stats">
      <span class="label">p50</span><span class="val">{fmtMs(p50())}</span>
      <span class="label">min</span><span class="val">{fmtMs(min())}</span>

      <span class="label">p95</span><span class="val">{fmtMs(p95())}</span>
      <span class="label warn">warn</span><span class="val warn">{fmtMs(warn())}</span>

      <span class="label">p99</span><span class="val">{fmtMs(p99())}</span>
      <span class="label">max</span><span class="val">{fmtMs(chartMax())}</span>

      <span class="label down col-right-start">timeout</span><span class="val down">{fmtMs(timeout())}</span>
    </div>
  );
};
