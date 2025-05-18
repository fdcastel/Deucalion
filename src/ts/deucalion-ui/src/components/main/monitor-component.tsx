import { useState, useEffect } from "react";
import { Box, Center, Flex, Image, Link, Spacer, Tag, Text } from "@chakra-ui/react";
import { Tooltip } from "../ui/tooltip";

import { MonitorState, type MonitorProps } from "../../services";
import { formatLastSeen, formatMonitorEvent, monitorStateToColor } from "../../services";

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
      bg={isFlashing ? "blue.subtle" : undefined}
      borderRadius="sm"
    >
      <Box hideBelow={"md"} hidden={!usingImages} marginLeft="0.5em">
        {config.image ? (
          <Image src={config.image} fit="contain" boxSize="2em" minWidth="2em" alt="icon" />
        ) : (
          <Box boxSize="2em" />
        )}
      </Box>

      <Tooltip showArrow content={formatLastSeen(lastState, monitor.stats)} disabled={lastState === MonitorState.Unknown} positioning={{ placement: "bottom-end" }}>
        <Text lineClamp={1} marginLeft="0.5em" minWidth={["6em", "6em", "8em"]} color={lastState !== MonitorState.Up ? monitorStateToColor(lastState) : undefined}>
          {config.href ? (
            <Link href={config.href} target="_blank" rel="noopener noreferrer">
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
            <Tooltip key={e.at} showArrow content={formatMonitorEvent(e)} positioning={{ placement: "top" }}>
              <Box
                bg={monitorStateToColor(e.st)}
                minWidth="0.5em"
                mr="0.25em"
                borderRadius="full"
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

        <Box hideBelow={"md"}>
          <Tooltip showArrow content="Availability" positioning={{ placement: "bottom-end" }}>
            <Tag.Root colorPalette="teal" variant="solid" borderRadius="full" marginRight="0.25em" >
              <Center minWidth="3em">{stats?.availability !== undefined ? stats.availability.toFixed(0) : "... "}%</Center>
            </Tag.Root>
          </Tooltip>
        </Box>

        <Tooltip showArrow content="Average response time" positioning={{ placement: "bottom-end" }}>
          <Tag.Root colorPalette="cyan" variant="solid" borderRadius="none" marginRight="0.5em">
            <Center minWidth="4em">{stats?.averageResponseTimeMs !== undefined ? stats.averageResponseTimeMs.toFixed(0) : "... "}ms</Center>
          </Tag.Root>
        </Tooltip>
      </Flex>
    </Flex>
  );
};
