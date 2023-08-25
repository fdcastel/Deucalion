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

export interface MonitorChangedDto {
  n: string;
  at: number;
  st: MonitorState;
}
