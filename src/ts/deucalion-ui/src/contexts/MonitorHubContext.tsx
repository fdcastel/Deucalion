import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

import { MonitorCheckedDto, MonitorEventDto, MonitorProps, MonitorStateChangedDto } from '../services';
import { monitorStateToDescription, monitorStateToStatus } from '../services';
import { logger } from '../services';

import { API_EVENTS_URL } from '../configuration';
import { useMonitors } from './MonitorsContext';

export const appendNewEvent = (monitors: Map<string, MonitorProps>, event: MonitorCheckedDto) => {
  const monitorName = event.n;
  const monitor = monitors.get(monitorName);

  if (monitor) {
    const existingEvents = monitor.events.filter((x) => x.at === event.at);
    if (existingEvents.length === 0) {
      const newMonitors = new Map(monitors);

      const newEvent = {
        at: event.at,
        st: event.st,
        ms: event.ms,
        te: event.te,
      } as MonitorEventDto;

      const sliced = monitor.events.slice(0, 59); // keep only the last 59 events (plus the new one = 60)

      const newMonitorProps = {
        name: monitorName,
        config: monitor.config,
        stats: event.ns,
        events: [newEvent, ...sliced],
      };

      newMonitors.set(monitorName, newMonitorProps);

      return newMonitors;
    }
  }

  return monitors;
};


interface IMonitorHubFacade {
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: Error | null;
}

const MonitorHubContext = createContext<IMonitorHubFacade | undefined>(undefined);

// Create the provider component
export const MonitorHubProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [readyState, setReadyState] = useState<number>(EventSource.CONNECTING);
  const [connectionError, setConnectionError] = useState<Error | null>(null);

  const { mutateMonitors } = useMonitors();

  useEffect(() => {
    setReadyState(EventSource.CONNECTING);

    const es = new EventSource(API_EVENTS_URL);

    // --- Event Handlers ---
    const handleMonitorChecked = (e: MessageEvent<string>) => {
      const event = JSON.parse(e.data) as MonitorCheckedDto;
      logger.log("[onMonitorChecked]", event);
      void mutateMonitors((oldMonitors) => (oldMonitors ? appendNewEvent(oldMonitors, event) : undefined), {
        revalidate: false,
        populateCache: true,
      });
    };

    const handleMonitorStateChanged = (e: MessageEvent<string>) => {
      const event = JSON.parse(e.data) as MonitorStateChangedDto;
      logger.log("[MonitorStateChanged]", event);
      const status = monitorStateToStatus(event.st);
      import("@heroui/react").then(({ toast }) => {
        toast(event.n, {
          description: monitorStateToDescription(event.st),
          variant: monitorStateToToastVariant(status),
        });
      }).catch((err) => {
        logger.error("Failed to toast state change", err);
      });
    };

    // --- Connection Lifecycle Handlers ---
    const handleOpen = () => {
      logger.log("SSE connection opened");
      setReadyState(EventSource.OPEN);
      setConnectionError(null);
    };

    const handleError = () => {
      logger.log("SSE connection error");
      setConnectionError(new Error("SSE connection error"));
      // EventSource sets readyState to CONNECTING before firing the error event
      // (the browser will auto-retry), so this reflects the retry state correctly.
      setReadyState(es.readyState);
    };

    es.addEventListener("MonitorChecked", handleMonitorChecked);
    es.addEventListener("MonitorStateChanged", handleMonitorStateChanged);
    es.addEventListener("open", handleOpen);
    es.addEventListener("error", handleError);

    // --- Cleanup on unmount ---
    return () => {
      logger.log("Closing SSE connection...");
      es.removeEventListener("MonitorChecked", handleMonitorChecked);
      es.removeEventListener("MonitorStateChanged", handleMonitorStateChanged);
      es.removeEventListener("open", handleOpen);
      es.removeEventListener("error", handleError);
      es.close();
      // Do not call setReadyState(CLOSED) here: state will be reset to CONNECTING
      // at the start of the next effect run (StrictMode re-mount or real re-mount).
    };
  }, [mutateMonitors]);

  // --- Context Value (Facade) ---
  const value: IMonitorHubFacade = {
    isConnected: readyState === EventSource.OPEN,
    isConnecting: readyState === EventSource.CONNECTING,
    connectionError,
  };

  return <MonitorHubContext.Provider value={value}>{children}</MonitorHubContext.Provider>;
};

export const useMonitorHubContext = (): IMonitorHubFacade => {
  const context = useContext(MonitorHubContext);

  if (context === undefined) {
    throw new Error('useMonitorHubContext must be used within a MonitorHubProvider');
  }

  return context;
};

function monitorStateToToastVariant(status: string): "success" | "warning" | "danger" | "default" {
  switch (status) {
    case "success":
      return "success";
    case "warning":
      return "warning";
    case "error":
      return "danger";
    default:
      return "default";
  }
}
