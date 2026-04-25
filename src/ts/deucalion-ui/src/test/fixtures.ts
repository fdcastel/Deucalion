import {
  MonitorState,
  type MonitorEventDto,
  type MonitorProps,
  type MonitorStatsDto,
} from "../services/deucalion-types";

// Convenience builders for tests. Times default to relative offsets from
// `now` so generated fixtures don't drift across runs.
export const SECONDS = 1;
export const MINUTES = 60;

const NOW = 1_700_000_000;

export const buildEvent = (over: Partial<MonitorEventDto> = {}): MonitorEventDto => ({
  at: NOW,
  st: MonitorState.Up,
  ms: 50,
  ...over,
});

export const buildEvents = (
  states: MonitorState[],
  opts: { stepSec?: number; ms?: (i: number, st: MonitorState) => number | undefined } = {},
): MonitorEventDto[] => {
  const step = opts.stepSec ?? 5;
  const msFn = opts.ms ?? ((_i, st) => (st === MonitorState.Down ? undefined : 50));
  // newest-first like the backend returns
  return states.map((st, i) => ({
    at: NOW - i * step,
    st,
    ms: msFn(i, st),
  }));
};

export const buildStats = (over: Partial<MonitorStatsDto> = {}): MonitorStatsDto => ({
  lastState: MonitorState.Up,
  lastUpdate: NOW,
  availability: 100,
  averageResponseTimeMs: 50,
  minResponseTimeMs: 40,
  latency50Ms: 50,
  latency95Ms: 70,
  latency99Ms: 90,
  ...over,
});

export const buildMonitor = (over: Partial<MonitorProps> = {}): MonitorProps => ({
  name: "test-monitor",
  config: { type: "http" },
  stats: buildStats(),
  events: buildEvents([MonitorState.Up, MonitorState.Up, MonitorState.Up]),
  ...over,
});

export { NOW as FIXED_NOW };
