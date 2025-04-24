import { useState, useEffect } from "react";
import { Box, Center, Flex, Hide, Image, Link, Spacer, Tag, Text, Tooltip } from "@chakra-ui/react";

import { MonitorState, MonitorProps } from "../../models";
import { formatLastSeen, formatMonitorEvent, monitorStateToColor } from "../../utils/formatting";

interface MonitorComponentProps {
  monitor: MonitorProps;
  usingImages?: boolean;
}

export const MonitorComponent = ({ monitor, usingImages }: MonitorComponentProps) => {
  const { name, config, stats, events } = monitor;
  const lastState = stats?.lastState ?? MonitorState.Unknown;

  const [isFlashing, setIsFlashing] = useState(false);

  useEffect(() => {
    if (events.length > 0) {
      setIsFlashing(true);

      const timer = setTimeout(() => {
        setIsFlashing(false);
      }, 500);    // Flash duration: 500ms

      // Cleanup timer
      return () => {
        clearTimeout(timer);
      };
    }
  }, [events]);

  return (
    <Flex
      alignItems="center"
      transition="background-color 0.5s ease-out"
      bg={isFlashing ? "flash" : "transparent"}
      p={1} // Add padding to make background visible
      borderRadius="md" // Add rounded corners
    >
      {usingImages ? (
        <Hide below="md" ssr={false}>
        {config.image ? (
          <Image src={config.image} boxSize="2em" marginRight="0.5em" minWidth="2em" alt="icon" />
        ) : (
          <Box boxSize="2em" marginRight="0.5em" />
        )}
      </Hide>
      ) : null}      

      <Tooltip hasArrow label={formatLastSeen(lastState, monitor.stats)} isDisabled={lastState === MonitorState.Unknown} placement="bottom-end">
        <Text noOfLines={1} minWidth={["6em", "6em", "8em"]} color={lastState !== MonitorState.Up ? monitorStateToColor(lastState) : undefined}>
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

      <Flex alignItems="center" overflowX="hidden">
        <Flex alignItems="center" height="2.5em" overflowX="hidden" flexDirection="row-reverse" justifyContent="flex-start" mr={1}>
          {events.map((e) => (
            <Tooltip key={e.at} hasArrow label={formatMonitorEvent(e)}>
              <Box
                bg={monitorStateToColor(e.st)}
                minWidth="0.5em"
                mr="0.25em"
                borderRadius="xl"
                height="1.5em"
                _hover={{
                  transform: "translateY(-0.25em)",
                  transitionDuration: "fast",
                  transitionTimingFunction: "ease-in-out",
                }}
              />
            </Tooltip>
          ))}
        </Flex>

        <Hide below="md" ssr={false}>
          <Tooltip hasArrow label="Availability" placement="bottom-end">
            <Tag colorScheme="teal" variant="solid" borderRadius="full" marginLeft="0.25em" minWidth="4em">
              <Center width="100%">{stats?.availability !== undefined ? stats.availability.toFixed(0) : "... "}%</Center>
            </Tag>
          </Tooltip>
        </Hide>

        <Tooltip hasArrow label="Average response time" placement="bottom-end">
          <Tag colorScheme="cyan" variant="solid" borderRadius="none" marginLeft="0.25em" minWidth="5em">
            <Center width="100%">{stats?.averageResponseTimeMs !== undefined ? stats.averageResponseTimeMs.toFixed(0) : "... "}ms</Center>
          </Tag>
        </Tooltip>
      </Flex>
    </Flex>
  );
};
