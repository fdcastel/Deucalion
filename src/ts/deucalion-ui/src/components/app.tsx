import { Container, Flex } from "@chakra-ui/react";

import { EMPTY_MONITORS } from "../services";
import { Overview, MonitorList } from "./main/index";

import { logger } from "../services";
import { preloadData, useData } from "../contexts/DataContext";
import { useMonitorHubContext } from "../contexts/MonitorHubContext";

preloadData();

if (import.meta.env.PROD) {
  logger.disableLogger();
}

export const App = () => {
  const { configurationData, monitorsData } = useData();
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
        <MonitorList monitors={monitorsData ?? EMPTY_MONITORS} />
      </Flex>
    </Container>
  );
};
