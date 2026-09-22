import { MonitorState, type MonitorEventDto, type MonitorDto, type MonitorStatsDto } from "./deucalion-types";

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
  end: number;   // epoch seconds: the newest event of the run
  durationSec: number; // start → end, or start → now while ongoing
  ageSec: number; // seconds since incident ended
  state: MonitorState;
  ongoing: boolean; // the run reaches the newest event: the monitor is still in it
  // The run reaches the oldest event known, so it may have begun before
  // `start`: the monitor has been in this state *at least* that long.
  startIsLowerBound: boolean;
}

// Walk events newest→oldest looking for the most recent run of Down/Degraded.
// Returns undefined if there's no incident in the window.
//
// The window is at most 120 probes -- twenty minutes at a 10s interval -- so
// for an ongoing Down run the start comes from the backend's `stats.since`,
// which spans the whole stored history: a monitor down for three days must
// not read as "down for 20m". Only Down is taken from there: the backend
// counts Degraded on the available side of its run, so it says nothing about
// where a Degraded run began. `stats.lastState` must agree with the window,
// or the stats are from before the newest event (GET reads them in separate
// queries; an SSE frame that only duplicates an event leaves them stale).
export const lastIncident = (
  events: MonitorEventDto[],
  nowEpoch?: number,
  stats?: Pick<MonitorStatsDto, "lastState" | "since" | "sinceIsLowerBound">,
): LastIncident | undefined => {
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
  const ongoing = endIdx === 0;
  const end = events[endIdx].at;
  let start = events[startIdx].at;
  let startIsLowerBound = startIdx === events.length - 1;
  if (
    ongoing &&
    incidentState === MonitorState.Down &&
    stats?.lastState === MonitorState.Down &&
    stats.since !== undefined
  ) {
    start = stats.since;
    startIsLowerBound = stats.sinceIsLowerBound ?? false;
  }
  const now = nowEpoch ?? Math.floor(Date.now() / 1000);
  return {
    start,
    end,
    durationSec: Math.max(0, (ongoing ? now : end) - start),
    ageSec: Math.max(0, now - end),
    state: incidentState,
    ongoing,
    startIsLowerBound,
  };
};

export interface AggregateAvailability {
  weightedAvailability: number;
  states: { up: number; warn: number; down: number; degraded: number; unknown: number };
  total: number;
}

// Aggregate availability across all monitors. We use stats.availability when
// present and fall back to computing from events.
export const aggregateAvailability = (monitors: MonitorDto[]): AggregateAvailability => {
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
