import { useEffect, useState } from "react";
import { Container, useToast } from "@chakra-ui/react";
import { createSignalRContext } from "react-signalr";

import { MonitorChangedDto, MonitorEventDto, monitorStateToDescription, monitorStateToStatus, EMPTY_MONITORS } from "../models";
import { Header, Overview, MonitorList } from "./main/index";

import { appendNewEvent, fetchMonitors, logger } from "../services";

const SignalRContext = createSignalRContext();

if (import.meta.env.PROD) {
  // Disables logger in production mode.
  logger.disableLogger();
}

const DEUCALION_PAGE_TITLE = import.meta.env.DEUCALION_PAGE_TITLE as string;

const API_URL = "/api/monitors/*";
const HUB_URL = "/api/monitors/hub";

export const App = () => {
  const toast = useToast();
  const [monitors, setMonitors] = useState(EMPTY_MONITORS);
  const [hubConnectionError, setHubConnectionError] = useState<Error | undefined>(undefined);

  useEffect(() => {
    if (monitors.size === 0) {
      // Fetch initial data only once (when allMonitors is empty).
      logger.log("Fetching initial data.");
      fetchMonitors(API_URL)
        .then((initialMonitors) => {
          setMonitors(initialMonitors);
        })
        .catch(() => {
          setMonitors(EMPTY_MONITORS);
        });
    }
  }, [monitors.size]);

  SignalRContext.useSignalREffect(
    "MonitorChecked",
    (newEvent: MonitorEventDto) => {
      logger.log("[onMonitorChecked] e=", newEvent);
      setMonitors((monitors) => appendNewEvent(monitors, newEvent));
    },
    []
  );

  SignalRContext.useSignalREffect(
    "MonitorChanged",
    (e: MonitorChangedDto) => {
      logger.log("[MonitorChanged]", e);
      toast({
        title: e.n,
        description: monitorStateToDescription(e.st),
        status: monitorStateToStatus(e.st),
        position: "bottom-right",
        variant: "left-accent",
        isClosable: true,
      });
    },
    []
  );

  return (
    <SignalRContext.Provider
      url={HUB_URL.toString()}
      onOpen={(connection) => {
        logger.warn("onOpen", connection);
        setHubConnectionError(undefined);
      }}
      onClosed={(error) => {
        logger.warn("onClosed", error);
        setHubConnectionError(error);
      }}
      onError={(error) => {
        logger.warn("onError", error);
        setHubConnectionError(error);
        return Promise.resolve();
      }}
      onReconnect={(connection) => {
        logger.warn("onReconnect", connection);
        setHubConnectionError(undefined);
      }}
    >
      <Container padding="4" maxWidth="80em">
        <Header title={DEUCALION_PAGE_TITLE} />
        <Overview monitors={monitors} hubConnection={SignalRContext.connection} hubConnectionError={hubConnectionError} />
        <MonitorList monitors={monitors} />
      </Container>
    </SignalRContext.Provider>
  );
};
