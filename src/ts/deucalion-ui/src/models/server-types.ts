export enum MonitorState {
  Unknown = -1,
  Down = 0,
  Up = 1,
  Warn = 2,
}

export interface MonitorInfoDto {
  group?: string;
  href?: string;
  image?: string;
}

export interface MonitorEventDto {
  n?: string;
  at: number;
  st: MonitorState;
  ms?: number;
  te?: string;
}

export interface MonitorStateChangedDto {
  n: string;
  at: number;
  st: MonitorState;
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
      return "---";
  }
};
