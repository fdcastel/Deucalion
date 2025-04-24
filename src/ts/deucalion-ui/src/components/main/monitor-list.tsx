import { Box, Card, CardBody, CardHeader, Heading } from "@chakra-ui/react";

import { MonitorProps } from "../../services";
import { MonitorComponent } from "./monitor-component";

interface MonitorListProps {
  groupedMonitors: Map<string, MonitorProps[]> | undefined;
  usingImages: boolean;
}

export const MonitorList = ({ groupedMonitors, usingImages }: MonitorListProps) => {
  // Handle undefined case
  if (!groupedMonitors) {
    return null; // Or return a loading indicator/placeholder
  }

  return (
    <Box>
      {Array.from(groupedMonitors).map(([groupName, monitorsInGroup]) => (
        <Card key={groupName} marginBottom={["0.5em", "0.5em", "1em"]}>
          <CardHeader hidden={!groupName} paddingY="0.5em" paddingX="0.5em">
            <Heading size="lg" fontWeight="thin">
              {groupName}
            </Heading>
          </CardHeader>
          <CardBody paddingY="0" paddingX="0.5em">
            {monitorsInGroup.map((monitorProps) => (
              <MonitorComponent key={monitorProps.name} monitor={monitorProps} usingImages={usingImages} />
            ))}
          </CardBody>
        </Card>
      ))}
    </Box>
  );
};
