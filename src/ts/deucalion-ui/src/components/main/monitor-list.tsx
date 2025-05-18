import { Box, Card, Heading } from "@chakra-ui/react";

import { type MonitorProps } from "../../services";
import { MonitorComponent } from "./monitor-component";

interface MonitorListProps {
  groupedMonitors: Map<string, MonitorProps[]> | undefined;
  usingImages: boolean;
}

export const MonitorList = ({ groupedMonitors, usingImages }: MonitorListProps) =>
  groupedMonitors ? (
    <Box>
      {Array.from(groupedMonitors).map(([groupName, monitorsInGroup]) => (
        <Card.Root key={groupName} marginBottom={["0.5em", "0.5em", "1em"]} shadow="sm" borderRadius="none">
          <Card.Header hidden={!groupName} padding="0.5em">
            <Heading size={["2xl", "2xl", "3xl"]} fontWeight="thin">
              {groupName}
            </Heading>
          </Card.Header>
          <Card.Body paddingTop="0" paddingBottom="0.5em" paddingX="0">
            {monitorsInGroup.map((monitorProps) => (
              <MonitorComponent key={monitorProps.name} monitor={monitorProps} usingImages={usingImages} />
            ))}
          </Card.Body>
        </Card.Root>
      ))}
    </Box>
  ) : null;
