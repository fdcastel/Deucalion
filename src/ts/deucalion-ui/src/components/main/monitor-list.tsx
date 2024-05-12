import { Box, Card, CardBody, CardHeader, Heading, List, ListItem } from "@chakra-ui/react";

import { MonitorProps } from "../../models";
import { MonitorComponent } from "./monitor-component";

interface MonitorListProps {
  monitors: Map<string, MonitorProps>;
}

export const MonitorList = ({ monitors }: MonitorListProps) => {
  const monitorsArray = Array.from(monitors);
  const usingImages = monitorsArray.findIndex(([_, mp]) => mp.config.image) !== -1;

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
        <Card marginY="1em" key={groupName}>
          <CardHeader hidden={!groupName} padding="0.5em">
            <Heading size="lg" fontWeight="thin">
              {groupName}
            </Heading>
          </CardHeader>
          <CardBody paddingY="0.5em">
            <List spacing="1em">
              {Array.from(monitors).map(([monitorName, monitorProps]) => (
                <ListItem key={monitorName}>
                  <MonitorComponent monitor={monitorProps} usingImages={usingImages} />
                </ListItem>
              ))}
            </List>
          </CardBody>
        </Card>
      ))}
    </Box>
  );
};
