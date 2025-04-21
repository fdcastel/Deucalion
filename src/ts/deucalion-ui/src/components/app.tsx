import { useState, useEffect } from "react";
import { Container, useToast } from "@chakra-ui/react";
import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";

import { MonitorCheckedDto, MonitorStateChangedDto, monitorStateToDescription, monitorStateToStatus, EMPTY_MONITORS } from "../models";
import { Overview, MonitorList } from "./main/index";

import { appendNewEvent, configurationFetcher, monitorsFetcher, logger } from "../services";

import useSWR, { preload } from "swr";

const CONFIGURATION_URL = "/api/configuration";
const MONITORS_URL = "/api/monitors";
const HUB_URL = "/api/monitors/hub";

void preload(CONFIGURATION_URL, configurationFetcher);
void preload(MONITORS_URL, monitorsFetcher);

if (import.meta.env.PROD) {
  // Disables logger in production mode.
  logger.disableLogger();
}

const SWR_OPTIONS = { suspense: true, revalidateOnMount: false, revalidateIfStale: false, revalidateOnFocus: false, revalidateOnReconnect: false };

export const App = () => {
  const { data: configuration } = useSWR(CONFIGURATION_URL, configurationFetcher, SWR_OPTIONS);
  const { data: monitors, mutate: mutateMonitors } = useSWR(MONITORS_URL, monitorsFetcher, SWR_OPTIONS);

  const [hubConnection, setHubConnection] = useState<HubConnection | null>(null);
  const [hubConnectionError, setHubConnectionError] = useState<Error | undefined>(undefined);
  const toast = useToast();

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    setHubConnection(connection);

    // Define handlers
    const handleMonitorChecked = (e: MonitorCheckedDto) => {
      logger.log("[onMonitorChecked]", e);
      mutateMonitors((oldMonitors) => (oldMonitors ? appendNewEvent(oldMonitors, e) : undefined), { revalidate: false });
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

    // Register handlers
    connection.on("MonitorChecked", handleMonitorChecked);
    connection.on("MonitorStateChanged", handleMonitorStateChanged);

    // Handle connection lifecycle
    connection.onclose((error) => {
      logger.warn("Connection closed", error);
      setHubConnectionError(error);
      // Optionally set connection state if needed elsewhere
    });

    connection.onreconnecting((error) => {
      logger.warn("Connection reconnecting", error);
      setHubConnectionError(error);
      // Optionally set connection state
    });

    connection.onreconnected((connectionId) => {
      logger.warn("Connection reconnected", connectionId);
      setHubConnectionError(undefined);
      // Optionally set connection state
    });

    // Start the connection
    connection
      .start()
      .then(() => {
        logger.warn("Connection started");
        setHubConnectionError(undefined);
      })
      .catch((err) => {
        // Use logger.warn instead of logger.error
        logger.warn("Error starting connection:", err);
        setHubConnectionError(err);
      });

    // Cleanup on unmount
    return () => {
      // Remove handlers
      connection.off("MonitorChecked", handleMonitorChecked);
      connection.off("MonitorStateChanged", handleMonitorStateChanged);

      // Stop connection
      connection
        .stop()
        .then(() => logger.warn("Connection stopped"))
        .catch((err) => logger.warn("Error stopping connection:", err));
    };
  }, [mutateMonitors, toast]); // Add dependencies

  return (
    <Container padding="4" maxWidth="container.xl">
      <Overview title={configuration?.pageTitle ?? "Deucalion Status"} monitors={monitors ?? EMPTY_MONITORS} hubConnection={hubConnection} hubConnectionError={hubConnectionError} />
      <MonitorList monitors={monitors ?? EMPTY_MONITORS} />
    </Container>
  );
};
