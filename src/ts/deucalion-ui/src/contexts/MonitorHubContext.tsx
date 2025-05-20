import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { HubConnectionBuilder, LogLevel, HubConnectionState } from '@microsoft/signalr';
import { addToast } from "@heroui/react";

import { MonitorCheckedDto, MonitorEventDto, MonitorProps, MonitorStateChangedDto } from '../services';
import { monitorStateToDescription, monitorStateToStatus } from '../services';
import { logger } from '../services';

import { API_HUB_URL } from '../configuration';
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
  isConnecting: boolean; // Includes connecting and reconnecting states
  connectionError: Error | null;
}

const MonitorHubContext = createContext<IMonitorHubFacade | undefined>(undefined);

// Create the provider component
export const MonitorHubProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [hubConnectionState, setHubConnectionState] = useState<HubConnectionState>(HubConnectionState.Disconnected);
  const [hubConnectionError, setHubConnectionError] = useState<Error | null>(null);

  const { mutateMonitors } = useMonitors(); 

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(API_HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    setHubConnectionState(HubConnectionState.Connecting);

    // --- Event Handlers ---
    const handleMonitorChecked = (e: MonitorCheckedDto) => {
      logger.log("[onMonitorChecked]", e);
      void mutateMonitors((oldMonitors) => (oldMonitors ? appendNewEvent(oldMonitors, e) : undefined), { revalidate: false });
    };

    const handleMonitorStateChanged = (e: MonitorStateChangedDto) => {
      logger.log("[MonitorStateChanged]", e);
      const status = monitorStateToStatus(e.st);
      addToast({
        title: e.n,
        description: monitorStateToDescription(e.st),
        color: monitorStateToHeroUIColor(status),
      });
    };

    connection.on("MonitorChecked", handleMonitorChecked);
    connection.on("MonitorStateChanged", handleMonitorStateChanged);

    // --- Connection Lifecycle Handlers ---
    connection.onclose((error: Error | undefined) => {
      logger.warn("Connection closed", error);
      setHubConnectionState(HubConnectionState.Disconnected);
      setHubConnectionError(error ?? null);
    });

    connection.onreconnecting((error: Error | undefined) => {
      logger.warn("Connection reconnecting", error);
      setHubConnectionState(HubConnectionState.Reconnecting);
      setHubConnectionError(error ?? null);
    });

    connection.onreconnected((connectionId?: string) => {
      logger.warn("Connection reconnected", connectionId);
      setHubConnectionState(HubConnectionState.Connected);
      setHubConnectionError(null);
    });

    // --- Start Connection ---
    connection
      .start()
      .then(() => {
        logger.warn("Connection started");
        setHubConnectionState(HubConnectionState.Connected);
        setHubConnectionError(null);
      })
      .catch((err: unknown) => {
        logger.warn("Error starting connection:", err);
        setHubConnectionState(HubConnectionState.Disconnected);
        if (err instanceof Error) {
          setHubConnectionError(err);
        } else {
          const errorMessage = typeof err === "string" ? err : JSON.stringify(err ?? "Unknown error starting connection");
          setHubConnectionError(new Error(errorMessage));
        }
      });

    // --- Cleanup on unmount ---
    return () => {
      logger.warn("Stopping connection...");
      connection.off("MonitorChecked", handleMonitorChecked);
      connection.off("MonitorStateChanged", handleMonitorStateChanged);
      connection
        .stop()
        .then(() => { 
          logger.warn("Connection stopped"); 
          setHubConnectionState(HubConnectionState.Disconnected);
        })
        .catch((err: unknown) => { 
          logger.warn("Error stopping connection:", err); 
        });
    };
  }, [mutateMonitors]);

  // --- Context Value (Facade) ---
  const value: IMonitorHubFacade = {
    isConnected: hubConnectionState === HubConnectionState.Connected,
    isConnecting: hubConnectionState === HubConnectionState.Connecting || hubConnectionState === HubConnectionState.Reconnecting,
    connectionError: hubConnectionError,
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

function monitorStateToHeroUIColor(status: string): "success" | "warning" | "danger" | "default" {
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
