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
  const { configuration: configurationResponse, monitors: monitorsResponse } = useData();
  const { data: configuration } = configurationResponse;
  const { data: monitors } = monitorsResponse;

  const { hubConnectionState, hubConnectionError } = useMonitorHubContext();

  return (
    <Container maxWidth="container.xl" padding="0">
      <Flex direction="column" padding="2">
        <Overview
          title={configuration?.pageTitle ?? "Deucalion Status"}
          monitors={monitors ?? EMPTY_MONITORS}
          hubConnectionState={hubConnectionState}
          hubConnectionError={hubConnectionError}
        />
        <MonitorList monitors={monitors ?? EMPTY_MONITORS} />
      </Flex>
    </Container>
  );
};
