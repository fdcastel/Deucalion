import { useEffect } from "react";
import { HubConnectionState } from "@microsoft/signalr"; 
import { Box, Flex, Hide, Image, Spacer, Stat, StatArrow, StatGroup, StatHelpText, StatLabel, StatNumber, Text, Tooltip } from "@chakra-ui/react";

import { ThemeSwitcher } from "./theme-switcher";

import { MonitorState, MonitorProps, dateTimeFromNow, dateTimeToString } from "../../services";

interface OverviewProps {
  title: string;
  monitors: Map<string, MonitorProps>;
  hubConnectionState: HubConnectionState; 
  hubConnectionError: Error | null;
}

export const Overview = ({ title, monitors, hubConnectionState, hubConnectionError }: OverviewProps) => {
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

  return (
    <Flex direction="column">
      <Flex alignItems="center">
        <Image src="/assets/deucalion-icon.svg" boxSize="3em" marginRight="0.5em" alt="icon" />
        <Text fontSize="3xl" noOfLines={1}>
          {title}
        </Text>
        <Spacer />
        <ThemeSwitcher />
      </Flex>

      <StatGroup marginY="1em" padding="0.5em" paddingBottom="0" bg="blackAlpha.200" boxShadow="md" borderRadius="md">
        <Stat>
          <StatLabel>Services</StatLabel>
          <Box filter="auto" blur={onlineServicesCount === 0 ? "6px" : "0px"}>
            <StatNumber>
              {onlineServicesCount} of {allServicesCount}
            </StatNumber>
          </Box>

          {onlineServicesCount === 0 ? (
            <StatHelpText textColor="gray">Loading...</StatHelpText>
          ) : onlineServicesCount === allServicesCount ? (
            <StatHelpText>Online</StatHelpText>
          ) : (
            <StatHelpText color="monitor.down">Degraded</StatHelpText>
          )}
        </Stat>

        <Stat>
          <StatLabel>Availability</StatLabel>
          <Box filter="auto" blur={isNaN(totalAvailability) ? "6px" : "0px"}>
            <StatNumber>{totalAvailability.toFixed(1)}%</StatNumber>
          </Box>
          <Box filter="auto" blur={firstUpdateAt === Number.MAX_VALUE ? "6px" : "0px"}>
            <StatHelpText>From {dateTimeFromNow(firstUpdateAt, true)}</StatHelpText>
          </Box>
        </Stat>

        <Hide below="md" ssr={false}>
          <Stat>
            <StatLabel>Updated</StatLabel>
            <Box filter="auto" blur={lastUpdateAt === 0 ? "6px" : "0px"}>
              <Tooltip hasArrow label={dateTimeToString(lastUpdateAt)} placement="left">
                <StatNumber noOfLines={1}>{dateTimeFromNow(lastUpdateAt)}</StatNumber>
              </Tooltip>
            </Box>
            <Tooltip hasArrow label={hubConnectionError?.message} isDisabled={hubConnectionError === null} placement="left">
              <StatHelpText>
                <StatArrow type={hubConnectionState === HubConnectionState.Connected ? "increase" : "decrease"} />
                {hubConnectionState}
              </StatHelpText>
            </Tooltip>
          </Stat>
        </Hide>
      </StatGroup>
    </Flex>
  );
};
