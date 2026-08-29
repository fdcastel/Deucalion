import { createSignal } from "solid-js";

import { API_EVENTS_URL } from "../configuration";
import type { MonitorCheckedDto, MonitorStateChangedDto } from "../services/deucalion-types";
import * as logger from "../services/logger";
import { mergeChecked, refetchMonitors } from "./monitors-store";
import { showStateChangeToast } from "./toast-store";

export type SseStatus = "connecting" | "open" | "error";

const [status, setStatus] = createSignal<SseStatus>("connecting");
export const sseStatus = status;

let activeSource: EventSource | null = null;

// True once any EventSource in this page's lifetime has reached `open`. A
// later `open` -- on the same source after the browser's automatic retry, or
// on a new source after a fatal error -- is a reconnect, and every event
// broadcast in between was missed (#18).
let hasOpenedBefore = false;

let visibilityListenerInstalled = false;

export const __resetSseForTests = (): void => {
  if (activeSource !== null) {
    activeSource.close();
    activeSource = null;
  }
  hasOpenedBefore = false;
  setStatus("connecting");
};

// Parse an SSE payload, returning null (and logging) instead of throwing:
// one malformed frame must not take the listener down with it.
const parsePayload = (e: MessageEvent<string>): unknown => {
  try {
    return JSON.parse(e.data) as unknown;
  } catch (err) {
    logger.warn(`SSE: ignoring malformed ${e.type} payload`, err);
    return null;
  }
};

// When the tab comes back into view after a fatal SSE error, try again. The
// browser only retries on its own while the source is CONNECTING; a CLOSED
// source stays closed. Reconnecting through connectSSE() also resyncs the
// store via the reconnect path in handleOpen.
const installVisibilityReconnect = (): void => {
  if (visibilityListenerInstalled || typeof document === "undefined") return;
  visibilityListenerInstalled = true;
  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible" && status() === "error") {
      logger.log("SSE: tab visible after error; reconnecting");
      connectSSE();
    }
  });
};

export const connectSSE = (): (() => void) => {
  if (activeSource !== null) {
    return () => { /* no-op: already connected */ };
  }

  installVisibilityReconnect();

  setStatus("connecting");
  const es = new EventSource(API_EVENTS_URL);
  activeSource = es;

  const handleChecked = (e: MessageEvent<string>): void => {
    const event = parsePayload(e) as MonitorCheckedDto | null;
    if (event) mergeChecked(event);
  };

  const handleStateChanged = (e: MessageEvent<string>): void => {
    const event = parsePayload(e) as MonitorStateChangedDto | null;
    if (event) showStateChangeToast(event);
  };

  const handleOpen = (): void => {
    if (hasOpenedBefore) {
      // Events broadcast while we were disconnected are gone for good; the
      // REST snapshot is the only way to close the gap in the heartbeat strip.
      logger.log("SSE connection re-opened; resyncing monitors");
      refetchMonitors();
    } else {
      logger.log("SSE connection opened");
    }
    hasOpenedBefore = true;
    setStatus("open");
  };

  const handleError = (): void => {
    if (es.readyState === EventSource.CLOSED) {
      // Fatal: the browser will not retry this source. Drop it so a later
      // connectSSE() (visibility change, retry affordance) can create a new one.
      logger.warn("SSE connection closed");
      dispose();
      setStatus("error");
    } else {
      logger.warn("SSE connection error; browser is retrying");
      setStatus("connecting");
    }
  };

  const dispose = (): void => {
    es.removeEventListener("MonitorChecked", handleChecked);
    es.removeEventListener("MonitorStateChanged", handleStateChanged);
    es.removeEventListener("open", handleOpen);
    es.removeEventListener("error", handleError);
    es.close();
    if (activeSource === es) activeSource = null;
  };

  es.addEventListener("MonitorChecked", handleChecked);
  es.addEventListener("MonitorStateChanged", handleStateChanged);
  es.addEventListener("open", handleOpen);
  es.addEventListener("error", handleError);

  return dispose;
};
