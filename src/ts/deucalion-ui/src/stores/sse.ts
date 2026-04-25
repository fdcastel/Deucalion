import { createSignal } from "solid-js";

import { API_EVENTS_URL } from "../configuration";
import type { MonitorCheckedDto, MonitorStateChangedDto } from "../services/deucalion-types";
import * as logger from "../services/logger";
import { mergeChecked } from "./monitors-store";
import { onMonitorChecked, onMonitorStateChanged } from "./events-store";
import { showStateChangeToast } from "./toast-store";

export type SseStatus = "connecting" | "open" | "error";

const [status, setStatus] = createSignal<SseStatus>("connecting");
export const sseStatus = status;

let activeSource: EventSource | null = null;

export const connectSSE = (): (() => void) => {
  if (activeSource !== null) {
    return () => { /* no-op: already connected */ };
  }

  setStatus("connecting");
  const es = new EventSource(API_EVENTS_URL);
  activeSource = es;

  const handleChecked = (e: MessageEvent<string>): void => {
    const event = JSON.parse(e.data) as MonitorCheckedDto;
    mergeChecked(event);
    onMonitorChecked(event);
  };

  const handleStateChanged = (e: MessageEvent<string>): void => {
    const event = JSON.parse(e.data) as MonitorStateChangedDto;
    onMonitorStateChanged(event);
    showStateChangeToast(event);
  };

  const handleOpen = (): void => {
    logger.log("SSE connection opened");
    setStatus("open");
  };

  const handleError = (): void => {
    logger.warn("SSE connection error");
    setStatus(es.readyState === EventSource.CLOSED ? "error" : "connecting");
  };

  es.addEventListener("MonitorChecked", handleChecked);
  es.addEventListener("MonitorStateChanged", handleStateChanged);
  es.addEventListener("open", handleOpen);
  es.addEventListener("error", handleError);

  return (): void => {
    es.removeEventListener("MonitorChecked", handleChecked);
    es.removeEventListener("MonitorStateChanged", handleStateChanged);
    es.removeEventListener("open", handleOpen);
    es.removeEventListener("error", handleError);
    es.close();
    activeSource = null;
  };
};
