import { MonitorState, MonitorEventDto, MonitorStatsDto } from "./deucalion-types";
import { dateTimeFromNow } from ".";

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

export const formatMonitorEvent = (e: MonitorEventDto) => (
  <div>
    <span className="text-bold" hidden={!e.ms}>{e.ms}ms </span>
    <span className="text-tiny text-gray-500">{dateTimeFromNow(e.at)}</span>
    <div className="text-xs">{e.te}</div>
  </div>
);

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

export const monitorStateToColor = (state?: MonitorState) => {
  switch (state) {
    case MonitorState.Up:
      return "monitor-up";
    case MonitorState.Warn:
      return "monitor-warn";
    case MonitorState.Degraded:
      return "monitor-degraded";
    case MonitorState.Down:
      return "monitor-down";
    default:
      return "monitor-unknown";
  }
};
