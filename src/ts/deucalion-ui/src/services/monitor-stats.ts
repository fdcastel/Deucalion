import { MonitorState, type MonitorEventDto, type MonitorProps } from "./deucalion-types";

// Pure helpers over MonitorEventDto[]. The backend returns events newest-first,
// and we keep that convention internally.

const COUNTABLE_STATES = new Set([MonitorState.Up, MonitorState.Warn, MonitorState.Down, MonitorState.Degraded]);

// Latency stats only consider successful probes — a Down probe's recorded
// timing (e.g. PingMonitor reporting 0ms when the OS rejects synchronously,
// or HTTP 5xx returned quickly) isn't a healthy-latency sample.
const isHealthyLatency = (st: MonitorState): boolean =>
  st === MonitorState.Up || st === MonitorState.Warn;

export const avail = (events: MonitorEventDto[]): number => {
  let total = 0;
  let down = 0;
  for (const e of events) {
    if (!COUNTABLE_STATES.has(e.st)) continue;
    total++;
    if (e.st === MonitorState.Down) down++;
  }
  if (total === 0) return 100;
  return ((total - down) / total) * 100;
};

export const avgMs = (events: MonitorEventDto[]): number | undefined => {
  let sum = 0;
  let n = 0;
  for (const e of events) {
    if (e.ms != null && isHealthyLatency(e.st)) {
      sum += e.ms;
      n++;
    }
  }
  return n > 0 ? sum / n : undefined;
};

export const minMs = (events: MonitorEventDto[]): number | undefined => {
  let min: number | undefined;
  for (const e of events) {
    if (e.ms != null && isHealthyLatency(e.st) && (min === undefined || e.ms < min)) min = e.ms;
  }
  return min;
};

// Nearest-rank percentile (0..1). Returns undefined for empty input.
export const percentile = (events: MonitorEventDto[], p: number): number | undefined => {
  const values: number[] = [];
  for (const e of events) {
    if (e.ms != null && isHealthyLatency(e.st)) values.push(e.ms);
  }
  if (values.length === 0) return undefined;
  values.sort((a, b) => a - b);
  let rank = Math.ceil(p * values.length);
  if (rank < 1) rank = 1;
  if (rank > values.length) rank = values.length;
  return values[rank - 1];
};

export interface LastIncident {
  start: number; // epoch seconds
  end: number;   // epoch seconds
  durationSec: number;
  ageSec: number; // seconds since incident ended
  state: MonitorState;
}

// Walk events newest→oldest looking for the most recent run of Down/Degraded.
// Returns undefined if there's no incident in the window.
export const lastIncident = (events: MonitorEventDto[], nowEpoch?: number): LastIncident | undefined => {
  if (events.length === 0) return undefined;
  let endIdx = -1;
  for (let i = 0; i < events.length; i++) {
    const s = events[i].st;
    if (s === MonitorState.Down || s === MonitorState.Degraded) { endIdx = i; break; }
  }
  if (endIdx === -1) return undefined;
  let startIdx = endIdx;
  const incidentState = events[endIdx].st;
  for (let i = endIdx + 1; i < events.length; i++) {
    const s = events[i].st;
    if (s === incidentState) startIdx = i;
    else break;
  }
  const end = events[endIdx].at;
  const start = events[startIdx].at;
  const now = nowEpoch ?? Math.floor(Date.now() / 1000);
  return {
    start,
    end,
    durationSec: Math.max(0, end - start),
    ageSec: Math.max(0, now - end),
    state: incidentState,
  };
};

export interface AggregateAvailability {
  weightedAvailability: number;
  states: { up: number; warn: number; down: number; degraded: number; unknown: number };
  total: number;
}

// Aggregate availability across all monitors. We use stats.availability when
// present and fall back to computing from events.
export const aggregateAvailability = (monitors: MonitorProps[]): AggregateAvailability => {
  let sum = 0;
  let up = 0, warn = 0, down = 0, degraded = 0, unknown = 0;
  for (const m of monitors) {
    sum += m.stats?.availability ?? avail(m.events);
    const last = m.stats?.lastState ?? (m.events[0]?.st ?? MonitorState.Unknown);
    switch (last) {
      case MonitorState.Up: up++; break;
      case MonitorState.Warn: warn++; break;
      case MonitorState.Down: down++; break;
      case MonitorState.Degraded: degraded++; break;
      default: unknown++; break;
    }
  }
  const total = monitors.length;
  return {
    weightedAvailability: total > 0 ? sum / total : 100,
    states: { up, warn, down, degraded, unknown },
    total,
  };
};
