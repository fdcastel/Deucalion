import { Container, Flex } from "@chakra-ui/react";

import { EMPTY_MONITORS } from "../services";
import { Overview, MonitorList } from "./main/index";

import { logger } from "../services";

import { preloadConfiguration, useConfiguration } from "../contexts/ConfigurationContext";
import { preloadMonitors, useMonitors } from "../contexts/MonitorsContext";
import { useMonitorHubContext } from "../contexts/MonitorHubContext";

preloadConfiguration();
preloadMonitors();

if (import.meta.env.PROD) {
  logger.disableLogger();
}

export const App = () => {
  const { configurationData } = useConfiguration();
  const { monitorsData, groupedMonitorsData, usingImages } = useMonitors();
  const { isConnected, isConnecting, connectionError } = useMonitorHubContext();

  return (
    <Container maxWidth="container.xl" padding="0">
      <Flex direction="column" padding="2">
        <Overview
          title={configurationData?.pageTitle ?? "Deucalion Status"}
          monitors={monitorsData ?? EMPTY_MONITORS}
          isConnected={isConnected}
          isConnecting={isConnecting}
          connectionError={connectionError}
        />
        <MonitorList groupedMonitors={groupedMonitorsData} usingImages={usingImages} />
      </Flex>
    </Container>
  );
};
