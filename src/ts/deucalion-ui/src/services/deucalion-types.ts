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
}

export interface MonitorEventDto {
  at: number;
  st: MonitorState;
  ms?: number;
}

export interface MonitorCheckedDto {
  n: string;
  at: number;
  fr: MonitorState;
  st: MonitorState;
  ms?: number;
  ns: MonitorStatsDto;
}

export interface MonitorStateChangedDto {
  n: string;
  at: number;
  fr: MonitorState;
  st: MonitorState;
}

export interface MonitorProps {
  name: string;
  config: MonitorConfigurationDto;
  stats?: MonitorStatsDto;
  events: MonitorEventDto[];
}

export interface PageConfigurationDto {
  pageTitle: string;
}
