import { MonitorState } from "./deucalion-types";

export const fmtMs = (ms?: number): string => {
  if (ms == null) return "—";
  if (ms < 1000) return `${Math.round(ms).toString()}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
};

export const fmtAgo = (epochSeconds: number): string => {
  const diff = Date.now() / 1000 - epochSeconds;
  if (diff < 5) return "just now";
  if (diff < 60) return `${Math.round(diff).toString()}s ago`;
  if (diff < 3600) return `${Math.round(diff / 60).toString()}m ago`;
  if (diff < 86400) return `${Math.round(diff / 3600).toString()}h ago`;
  return `${Math.round(diff / 86400).toString()}d ago`;
};

export const fmtDur = (seconds: number): string => {
  if (seconds < 60) return `${Math.round(seconds).toString()}s`;
  if (seconds < 3600) return `${Math.round(seconds / 60).toString()}m`;
  if (seconds < 86400) return `${(seconds / 3600).toFixed(1)}h`;
  return `${(seconds / 86400).toFixed(1)}d`;
};

export const fmtTime = (epochSeconds: number): string => {
  const d = new Date(epochSeconds * 1000);
  return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false });
};

export const stateName = (s: MonitorState): string => {
  switch (s) {
    case MonitorState.Up: return "up";
    case MonitorState.Down: return "down";
    case MonitorState.Warn: return "warn";
    case MonitorState.Degraded: return "degraded";
    default: return "unknown";
  }
};

export const stateLabel = (s: MonitorState): string => {
  switch (s) {
    case MonitorState.Up: return "Up";
    case MonitorState.Down: return "Down";
    case MonitorState.Warn: return "Warn";
    case MonitorState.Degraded: return "Degraded";
    default: return "Unknown";
  }
};

export const monitorStateToDescription = (state: MonitorState): string => {
  switch (state) {
    case MonitorState.Up: return "Is online.";
    case MonitorState.Warn: return "Changed to warning.";
    case MonitorState.Down: return "Is down.";
    case MonitorState.Degraded: return "May be down.";
    default: return "Is unknown.";
  }
};

export type ToastVariant = "up" | "warn" | "down" | "degraded" | "default";

export const monitorStateToToastVariant = (state: MonitorState): ToastVariant => {
  switch (state) {
    case MonitorState.Up: return "up";
    case MonitorState.Warn: return "warn";
    case MonitorState.Down: return "down";
    case MonitorState.Degraded: return "degraded";
    default: return "default";
  }
};
