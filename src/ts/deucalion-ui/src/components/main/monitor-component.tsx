import { Box, Center, Flex, Hide, Image, Link, Spacer, Tag, Text, Tooltip } from "@chakra-ui/react";

import { dateTimeFromNow } from "../../services";
import { MonitorCheckedDto, MonitorState, MonitorProps } from "../../models";

const formatMonitorEvent = (e: MonitorCheckedDto) => {
  const at = dateTimeFromNow(e.at);
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

interface MonitorComponentProps {
  monitor: MonitorProps;
  usingImages?: boolean;
}

export const MonitorComponent = ({ monitor, usingImages }: MonitorComponentProps) => {
  const { name, config, events, stats } = monitor;

  const textOffset = usingImages && !config.image ? "2em" : "0.5em";

  const reverseEvents = events.map((_, idx) => events[events.length - 1 - idx]);
  return (
    <Flex>
      {config.image ? <Image src={config.image} width="1.5em" height="1.5em" alt="icon" /> : <div />}
      <Text
        marginLeft={textOffset}
        noOfLines={1}
        minWidth="8em"
        color={stats?.lastState !== MonitorState.Up ? monitorStateToColor(stats?.lastState) : undefined}
      >
        {config.href ? (
          <Link href={config.href} isExternal>
            {name}
          </Link>
        ) : (
          name
        )}
      </Text>
      <Spacer />

      {/* ToDo: 
            overflowX="clip" don't work in Firefox. 
            overflowX="hidden" works. But clip hover animation.
        */}
      <Flex direction="row-reverse" overflowX="clip">
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
