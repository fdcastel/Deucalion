import { MonitorCheckedDto, MonitorConfigurationDto, MonitorState, MonitorSummaryDto } from "./server-types";

export const EMPTY_MONITORS = new Map<string, MonitorProps>();

export interface MonitorStats {
  availability: number;
  averageResponseTime: number;
  lastState: MonitorState;
  lastUpdate: number;
}

export interface MonitorProps {
  name: string;
  config: MonitorConfigurationDto;
  summary: MonitorSummaryDto;
  events: MonitorCheckedDto[];
  stats?: MonitorStats;
}
