import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel, HubConnectionState } from '@microsoft/signalr';
import { useToast } from '@chakra-ui/react';

import { MonitorCheckedDto, MonitorEventDto, MonitorProps, MonitorStateChangedDto } from '../services';
import { monitorStateToDescription, monitorStateToStatus } from '../services';
import { logger } from '../services';

import { API_HUB_URL } from '../configuration';
import { useData } from './DataContext';

interface IMonitorHubContext {
  hubConnection: HubConnection | null;
  hubConnectionState: HubConnectionState;
  hubConnectionError: Error | null;
}

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


// Create the context
const MonitorHubContext = createContext<IMonitorHubContext | undefined>(undefined);

// Create the provider component
export const MonitorHubProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [hubConnection, setHubConnection] = useState<HubConnection | null>(null);
  const [hubConnectionState, setHubConnectionState] = useState<HubConnectionState>(HubConnectionState.Disconnected);
  const [hubConnectionError, setHubConnectionError] = useState<Error | null>(null);

  // Get mutateMonitors directly from the facade
  const { mutateMonitors } = useData(); 
  const toast = useToast();

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(API_HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    setHubConnection(connection);
    setHubConnectionState(HubConnectionState.Connecting);

    // --- Event Handlers ---
    const handleMonitorChecked = (e: MonitorCheckedDto) => {
      logger.log("[onMonitorChecked]", e);
      // Use mutateMonitors from the facade
      void mutateMonitors((oldMonitors) => (oldMonitors ? appendNewEvent(oldMonitors, e) : undefined), { revalidate: false });
    };

    const handleMonitorStateChanged = (e: MonitorStateChangedDto) => {
      logger.log("[MonitorStateChanged]", e);
      toast({
        title: e.n,
        description: monitorStateToDescription(e.st),
        status: monitorStateToStatus(e.st),
        position: "bottom-right",
        variant: "left-accent",
        isClosable: true,
      });
    };

    // Register SignalR event handlers
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
          // State might already be Disconnected due to onclose
        });
    };
  // Update dependency array
  }, [mutateMonitors, toast]); 

  // --- Context Value ---
  const value: IMonitorHubContext = {
    hubConnection,
    hubConnectionState,
    hubConnectionError,
  };

  return <MonitorHubContext.Provider value={value}>{children}</MonitorHubContext.Provider>;
};

// Create the custom hook to consume the context
export const useMonitorHubContext = () => {
  const context = useContext(MonitorHubContext);

  if (context === undefined) {
    throw new Error('useMonitorHubContext must be used within a MonitorHubProvider');
  }

  return context;
};
