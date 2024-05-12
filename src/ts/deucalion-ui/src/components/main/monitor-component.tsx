import { Box, Center, Flex, Hide, Image, Link, Spacer, Tag, Text, Tooltip } from "@chakra-ui/react";

import { dateTimeFromNow } from "../../services";
import { MonitorCheckedDto, MonitorState, MonitorProps, MonitorSummaryDto } from "../../models";

const formatLastSeen = (state: MonitorState, m: MonitorSummaryDto) => {
  switch (state) {
    case MonitorState.Up:
    case MonitorState.Warn:
      if (m.lastDown) {
        const lastDownAt = dateTimeFromNow(m.lastDown);
        return `Last down at: ${lastDownAt}`;
      }
      break;

    case MonitorState.Down:
      if (m.lastUp) {
        const lastUpAt = dateTimeFromNow(m.lastUp);
        return `Last up at: ${lastUpAt}`;
      }
      break;
  }
};

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
  const lastState = stats?.lastState ?? MonitorState.Unknown;
  const textOffset = usingImages && !config.image ? "2em" : "0.5em";

  const reverseEvents = events.map((_, idx) => events[events.length - 1 - idx]);
  return (
    <Flex>
      {config.image ? <Image src={config.image} width="1.5em" height="1.5em" alt="icon" /> : <div />}
      <Tooltip hasArrow label={formatLastSeen(lastState, monitor.summary)} isDisabled={lastState === MonitorState.Unknown} placement="bottom-end">
        <Text marginLeft={textOffset} noOfLines={1} minWidth="8em" color={lastState !== MonitorState.Up ? monitorStateToColor(lastState) : undefined}>
          {config.href ? (
            <Link href={config.href} isExternal>
              {name}
            </Link>
          ) : (
            name
          )}
        </Text>
      </Tooltip>
      <Spacer />

      {/* ToDo: overflowX="clip" doesn't work in Firefox. 
                overflowX="hidden" works. But clips hover animation. */}
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
