import { Badge, Box, Center, Flex, Spacer, Text, Tooltip } from "@chakra-ui/react";

import dayjs from "dayjs";
import { MonitorEventDto, MonitorState } from "../server-types";

export interface MonitorStats {
  availability: number;
  lastState: MonitorState;
  lastUpdate: number;
}

export interface MonitorProps {
  name: string;
  events: MonitorEventDto[];
  stats?: MonitorStats;
}

// --- MonitorComponent functions 

const formatMonitorEvent = (e: MonitorEventDto) => {
  const at = dayjs.unix(e.at).fromNow();
  const timeStamp = e.ms ? `${at}: ${e.ms}ms` : at;
  return e.te ? `${timeStamp} (${e.te})` : timeStamp;
};

const monitorStateToColor = (state: MonitorState) => {
  switch (state) {
    case MonitorState.Up:
      return "green.400";
    case MonitorState.Warn:
      return "yellow.400";
    case MonitorState.Down:
      return "red.400";
    default:
      return "gray.400";
  }
};

// --- MonitorComponent

export const MonitorComponent = ({ name, events, stats }: MonitorProps) => (
  <Flex>
    <Text noOfLines={1}>{name}</Text>
    <Spacer />
    {events.map((e) => (
      <Tooltip key={e.at} hasArrow label={formatMonitorEvent(e)}>
        <Box
          bg={monitorStateToColor(e.st)}
          minWidth="0.5em"
          mr="0.25em"
          borderRadius="xl"
          _hover={{
            transform: "translateY(-0.25em)",
            transitionDuration: "0.2s",
            transitionTimingFunction: "ease-in-out",
          }}
        />
      </Tooltip>
    ))}
    <Center minWidth="4em">
      <Badge colorScheme="teal" variant="solid" borderRadius="xl" paddingX="0.75em">
        {stats?.availability.toFixed(0)}%
      </Badge>
    </Center>
  </Flex>
);
