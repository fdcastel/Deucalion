export const enum MonitorState {
  Unknown = 0,
  Down = 1,
  Up = 2,
  Warn = 3,
  Degraded = 4,
}

export type MonitorType = "ping" | "http" | "dns" | "tcp" | "checkin" | "unknown";

export interface MonitorConfigurationDto {
  type: MonitorType;
  group?: string;
  href?: string;
}

export interface MonitorStatsDto {
  lastState: MonitorState;

  availability: number;

  minResponseTimeMs?: number;
  latency50Ms?: number;
  latency95Ms?: number;
  latency99Ms?: number;

  warnTimeoutMs?: number;
  timeoutMs?: number;
}

// One event as the UI works with it (decoded from the wire form below).
export interface MonitorEventDto {
  at: number;
  st: MonitorState;
  ms?: number;
}

// GET /api/monitors ships events columnar, newest first: `at` of events[0],
// `dt` seconds between consecutive events (one fewer than events), `st` one
// state digit per event, `ms` latency per event (null when the probe recorded
// none). Mirrors Deucalion.Api MonitorEventsDto; decoded by services/wire.ts.
export interface MonitorEventsDto {
  at: number;
  dt: number[];
  st: string;
  ms: (number | null)[];
}

export interface MonitorCheckedDto {
  n: string;
  at: number;
  st: MonitorState;
  ms?: number;
  ns: MonitorStatsDto;
}

export interface MonitorStateChangedDto {
  n: string;
  at: number;
  st: MonitorState;
}

export interface MonitorDto {
  name: string;
  config: MonitorConfigurationDto;
  stats?: MonitorStatsDto;
  events: MonitorEventDto[];
}

// The GET /api/monitors row as served; `events` is omitted when there are none.
export type MonitorWireDto = Omit<MonitorDto, "events"> & { events?: MonitorEventsDto };

export interface PageConfigurationDto {
  pageTitle: string;
}
