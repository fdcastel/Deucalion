export const EMPTY_MONITORS = new Map<string, MonitorProps>();

export enum MonitorState {
  Unknown = -1,
  Down = 0,
  Up = 1,
  Warn = 2,
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

export const monitorStateToStatus = (state: MonitorState) => {
  switch (state) {
    case MonitorState.Up:
      return "success";
    case MonitorState.Warn:
      return "warning";
    case MonitorState.Down:
      return "error";
    default:
      return "info";
  }
};

export const monitorStateToDescription = (state: MonitorState) => {
  switch (state) {
    case MonitorState.Up:
      return "Is online.";
    case MonitorState.Warn:
      return "Changed to warning.";
    case MonitorState.Down:
      return "Is down.";
    default:
      return "Is unknown";
  }
};
