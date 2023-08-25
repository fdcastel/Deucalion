import { Box, Center, Flex, Hide, Image, Link, Spacer, Tag, Text, Tooltip } from "@chakra-ui/react";

import dayjs from "dayjs";
import { MonitorEventDto, MonitorInfoDto, MonitorState } from "../server-types";

export interface MonitorStats {
  availability: number;
  averageResponseTime: number;
  lastState: MonitorState;
  lastUpdate: number;
}

export interface MonitorProps {
  name: string;
  info: MonitorInfoDto;
  events: MonitorEventDto[];
  stats?: MonitorStats;
}

const formatMonitorEvent = (e: MonitorEventDto) => {
  const at = dayjs.unix(e.at).fromNow();
  const timeStamp = e.ms ? `${at}: ${e.ms}ms` : at;
  return e.te ? `${timeStamp} (${e.te})` : timeStamp;
};

const monitorStateToColor = (state?: MonitorState) => {
  switch (state) {
    case MonitorState.Up:
      return "monitor.up";
    case MonitorState.Warn:
      return "monitor.warn";
    case MonitorState.Down:
      return "monitor.down";
    default:
      return "monitor.unknown";
  }
};

export const MonitorComponent = ({ name, info, events, stats }: MonitorProps) => {
  const reverseEvents = events.map((_, idx) => events[events.length - 1 - idx]);
  return (
    <Flex>
      {info.image ? <Image src={info.image} height="1.5em" marginRight="0.5em" /> : <div />}
      <Text noOfLines={1} minWidth="5em" color={stats?.lastState !== MonitorState.Up ? monitorStateToColor(stats?.lastState) : undefined}>
        {info.href ? (
          <Link href={info.href} isExternal>
            {name}
          </Link>
        ) : (
          name
        )}
      </Text>
      <Spacer />

      <Flex direction="row-reverse" overflowX={"clip"}>
        <Tooltip hasArrow label="Average response time" placement="bottom-end">
          <Tag colorScheme="cyan" variant="solid" borderRadius="lg" marginLeft="0.25em" minWidth="4em">
            <Center width="100%">{stats?.averageResponseTime !== undefined ? stats.averageResponseTime.toFixed(0) : "... "}ms</Center>
          </Tag>
        </Tooltip>

        <Hide below="md" ssr={false}>
          <Tooltip hasArrow label="Availability" placement="bottom-end">
            <Tag colorScheme="teal" variant="solid" borderRadius="full" marginLeft="0.25em" minWidth="4em">
              <Center width="100%">{stats?.availability !== undefined ? stats.availability.toFixed(0) : "... "}%</Center>
            </Tag>
          </Tooltip>
        </Hide>

        {reverseEvents.map((e) => (
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
      </Flex>
    </Flex>
  );
};
