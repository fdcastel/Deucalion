export const EMPTY_MONITORS = new Map<string, MonitorProps>();

export enum MonitorState {
  Unknown = 0,
  Down = 1,
  Up = 2,
  Warn = 3,
  Degraded = 4,
}

export interface MonitorConfigurationDto {
  group?: string;
  href?: string;
  image?: string;
}

export interface MonitorStatsDto {
  lastState: MonitorState,
  lastUpdate: number,

  availability: number,
  averageResponseTimeMs: number,

  lastSeenUp?: number;
  lastSeenDown?: number;
}

export interface MonitorCheckedDto {
  n: string;
  at: number;
  st: MonitorState;
  ms?: number;
  te?: string;
  ns: MonitorStatsDto
}

export interface MonitorStateChangedDto {
  n: string;
  at: number;
  st: MonitorState;
}

export interface MonitorEventDto {
  at: number;
  st: MonitorState;
  ms?: number;
  te?: string;
}

export interface MonitorProps {
  name: string;
  config: MonitorConfigurationDto;
  stats?: MonitorStatsDto;
  events: MonitorEventDto[];
}

export interface DeucalionOptions {
  pageTitle: string;
}
