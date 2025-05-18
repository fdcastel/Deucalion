import { useEffect } from "react";
import { Box, Flex, Image, Spacer, Stat, StatGroup, Text, StatLabel, StatHelpText, StatUpIndicator, StatDownIndicator, StatValueText } from "@chakra-ui/react";
import { Tooltip } from "../ui/tooltip";

import { MonitorState, type MonitorProps, dateTimeFromNow, dateTimeToString } from "../../services";
import { ColorModeButton } from "../ui/color-mode";

interface OverviewProps {
  title: string;
  monitors: Map<string, MonitorProps>;
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: Error | null;
}

export const Overview = ({ title, monitors, isConnected, isConnecting, connectionError }: OverviewProps) => {
  const allServicesCount = monitors.size;

  let firstUpdateAt = Number.MAX_VALUE;
  let lastUpdateAt = 0;

  let onlineServicesCount = 0;
  let eventCount = 0;
  let totalAvailability = 0;
  for (const [, mp] of monitors) {
    const isOnline = mp.stats?.lastState == MonitorState.Up || mp.stats?.lastState == MonitorState.Warn;

    onlineServicesCount += isOnline ? 1 : 0;

    eventCount += mp.events.length;
    totalAvailability += ((mp.stats?.availability ?? 0) * mp.events.length) / 100;

    firstUpdateAt = mp.events[0]?.at ? Math.min(firstUpdateAt, mp.events[0].at) : firstUpdateAt;
    lastUpdateAt = mp.stats?.lastUpdate ? Math.max(lastUpdateAt, mp.stats.lastUpdate) : lastUpdateAt;
  }
  totalAvailability = eventCount > 0 ? (100 * totalAvailability) / eventCount : 0;

  useEffect(() => {
    document.title = onlineServicesCount === allServicesCount ? title : `(-${String(allServicesCount - onlineServicesCount)}) ${title}`;
  }, [title, allServicesCount, onlineServicesCount]);

  const connectionStatusText = isConnected ? "Connected" : isConnecting ? "Connecting..." : "Disconnected";

  return (
    <Flex direction="column">
      <Flex alignItems="center">
        <Image src="/assets/deucalion-icon.svg" fit="contain" boxSize="3em" marginRight="0.5em" alt="icon" />
        <Text fontSize="3xl">
          {title}
        </Text>
        <Spacer />
        <ColorModeButton />
      </Flex>

      <StatGroup marginY="1em" padding="0.5em" bg="bg.emphasized" shadow="md" borderRadius="md">
        <Stat.Root>
          <StatLabel>Services</StatLabel>
          <Box filter="auto" blur={onlineServicesCount === 0 ? "6px" : "0px"}>
            <StatValueText>
              {onlineServicesCount} of {allServicesCount}
            </StatValueText>
          </Box>
          {onlineServicesCount === 0 ? (
            <StatHelpText>Loading...</StatHelpText>
          ) : onlineServicesCount === allServicesCount ? (
            <StatHelpText>Online</StatHelpText>
          ) : (
            <StatHelpText color="monitor.down">Degraded</StatHelpText>
          )}
        </Stat.Root>

        <Stat.Root>
          <StatLabel>Availability</StatLabel>
          <Box filter="auto" blur={isNaN(totalAvailability) ? "6px" : "0px"}>
            <StatValueText>{totalAvailability.toFixed(1)}%</StatValueText>
          </Box>
          <StatHelpText>From {dateTimeFromNow(firstUpdateAt, true)}</StatHelpText>
        </Stat.Root>

        <Stat.Root hideBelow={"md"}>
          <StatLabel>Updated</StatLabel>
          <Box filter="auto" blur={lastUpdateAt === 0 ? "6px" : "0px"}>
            <Tooltip showArrow content={dateTimeToString(lastUpdateAt)} positioning={{ placement: "left" }}>
              <StatValueText lineClamp={1}>{dateTimeFromNow(lastUpdateAt)}</StatValueText>
            </Tooltip>
          </Box>
          <Tooltip showArrow content={connectionError?.message} disabled={connectionError === null} positioning={{ placement: "left" }}>
            <StatHelpText>
              {isConnected ? <StatUpIndicator /> : <StatDownIndicator />}
              {connectionStatusText}
            </StatHelpText>
          </Tooltip>
        </Stat.Root>
      </StatGroup>
    </Flex>
  );
};
