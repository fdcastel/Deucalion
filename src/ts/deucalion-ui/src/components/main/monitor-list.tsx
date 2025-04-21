import { Box, Card, CardBody, CardHeader, Heading } from "@chakra-ui/react";

import { MonitorProps } from "../../models";
import { MonitorComponent } from "./monitor-component";

interface MonitorListProps {
  monitors: Map<string, MonitorProps>;
}

export const MonitorList = ({ monitors }: MonitorListProps) => {
  const monitorsArray = Array.from(monitors);
  const usingImages = monitorsArray.findIndex(([, mp]) => mp.config.image) !== -1;

  const groupedMonitors = monitorsArray.reduce((groups, [monitorName, monitorProps]) => {
    const groupKey = monitorProps.config.group ?? "";

    let slot = groups.get(groupKey);
    if (slot === undefined) {
      slot = new Map<string, MonitorProps>();
      groups.set(groupKey, slot);
    }
    slot.set(monitorName, monitorProps);

    return groups;
  }, new Map<string, Map<string, MonitorProps>>());

  return (
    <Box>
      {Array.from(groupedMonitors).map(([groupName, monitors]) => (
        <Card key={groupName} marginBottom={["0.5em", "0.5em", "1em"]}>
          <CardHeader hidden={!groupName} paddingY="0.5em" paddingX="0.5em">
            <Heading size="lg" fontWeight="thin">
              {groupName}
            </Heading>
          </CardHeader>
          <CardBody paddingY="0" paddingX="0.5em">
            {Array.from(monitors).map(([monitorName, monitorProps]) => (
              <MonitorComponent key={monitorName} monitor={monitorProps} usingImages={usingImages} />
            ))}
          </CardBody>
        </Card>
      ))}
    </Box>
  );
};
