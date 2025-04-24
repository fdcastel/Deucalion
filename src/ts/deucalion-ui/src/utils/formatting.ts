import { MonitorState, MonitorEventDto, MonitorStatsDto } from "../models/deucalion-types";
import { dateTimeFromNow } from "../services";

export const formatLastSeen = (state: MonitorState, m?: MonitorStatsDto) => {
  if (!m) {
    return undefined;
  }

  switch (state) {
    case MonitorState.Up:
    case MonitorState.Warn:
      if (m.lastSeenDown) {
        const lastSeenDownAt = dateTimeFromNow(m.lastSeenDown);
        return `Last seen down ${lastSeenDownAt}`;
      }
      break;

    case MonitorState.Down:
    case MonitorState.Degraded:
      if (m.lastSeenUp) {
        const lastSeenUpAt = dateTimeFromNow(m.lastSeenUp);
        return `Last seen up ${lastSeenUpAt}`;
      }
      break;
  }
};

export const formatMonitorEvent = (e: MonitorEventDto) => {
  const at = dateTimeFromNow(e.at);
  const timeStamp = e.ms ? `${at}: ${String(e.ms)}ms` : at;
  return e.te ? `${timeStamp} (${e.te})` : timeStamp;
};

export const monitorStateToColor = (state?: MonitorState) => {
  switch (state) {
    case MonitorState.Up:
      return "monitor.up";
    case MonitorState.Warn:
      return "monitor.warn";
    case MonitorState.Degraded:
      return "monitor.degraded";
    case MonitorState.Down:
      return "monitor.down";
    default:
      return "monitor.unknown";
  }
};

export const monitorStateToStatus = (state: MonitorState) => {
  switch (state) {
    case MonitorState.Up:
    case MonitorState.Degraded:
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
    case MonitorState.Degraded:
      return "May be down.";
    default:
      return "Is unknown";
  }
};
