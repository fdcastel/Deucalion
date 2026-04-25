import type { Component } from "solid-js";

import type { MonitorProps } from "../../services/deucalion-types";
import { fmtMs } from "../../services/formatting";
import { minMs, percentile } from "../../services/monitor-stats";

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

  return (
    <div class="lat-stats">
      <span>min <strong>{fmtMs(min())}</strong></span>
      <span>p50 <strong>{fmtMs(p50())}</strong></span>
      <span>p95 <strong>{fmtMs(p95())}</strong></span>
      <span>p99 <strong>{fmtMs(p99())}</strong></span>
    </div>
  );
};
