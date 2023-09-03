import { useState } from "react";
import { Container, useToast } from "@chakra-ui/react";
import { createSignalRContext } from "react-signalr";

import { MonitorEventDto, MonitorStateChangedDto, monitorStateToDescription, monitorStateToStatus, MonitorProps, EMPTY_MONITORS } from "../models";
import { Header, Overview, MonitorList } from "./main/index";

import { appendNewEvent, configurationFetcher, monitorsFetcher, logger } from "../services";

import useSWR, { preload } from "swr";

const CONFIGURATION_URL = "/api/configuration";
const MONITORS_URL = "/api/monitors/*";
const HUB_URL = "/api/monitors/hub";

void preload(CONFIGURATION_URL, configurationFetcher);
void preload(MONITORS_URL, monitorsFetcher);

const SignalRContext = createSignalRContext();

if (import.meta.env.PROD) {
  // Disables logger in production mode.
  logger.disableLogger();
}

const SWR_OPTIONS = { suspense: true, revalidateOnMount: false, revalidateIfStale: false, revalidateOnFocus: false, revalidateOnReconnect: false };

export const App = () => {
  const { data: configuration } = useSWR(CONFIGURATION_URL, configurationFetcher, SWR_OPTIONS);
  const { data: monitors, mutate: mutateMonitors } = useSWR(MONITORS_URL, monitorsFetcher, SWR_OPTIONS);

  const [hubConnectionError, setHubConnectionError] = useState<Error | undefined>(undefined);
  const toast = useToast();

  SignalRContext.useSignalREffect(
    "MonitorChecked",
    (newEvent: MonitorEventDto) => {
      logger.log("[onMonitorChecked] e=", newEvent);
      void mutateMonitors<Map<string, MonitorProps>>((oldMonitors) => (oldMonitors ? appendNewEvent(oldMonitors, newEvent) : undefined), { revalidate: false });
    },
    []
  );

  SignalRContext.useSignalREffect(
    "MonitorStateChanged",
    (e: MonitorStateChangedDto) => {
      logger.log("[MonitorStateChanged]", e);
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
        <Header title={configuration?.pageTitle ?? "Deucalion Status"} />
        <Overview monitors={monitors ?? EMPTY_MONITORS} hubConnection={SignalRContext.connection} hubConnectionError={hubConnectionError} />
        <MonitorList monitors={monitors ?? EMPTY_MONITORS} />
      </Container>
    </SignalRContext.Provider>
  );
};
