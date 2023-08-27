import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { Box, Stat, StatArrow, StatGroup, StatHelpText, StatLabel, StatNumber, Tooltip } from "@chakra-ui/react";

import { MonitorState, MonitorProps } from "../../models";
import { dateTimeFromNow, dateTimeToString } from "../../services";

interface OverviewProps {
  monitors: Map<string, MonitorProps>;
  hubConnection: HubConnection | null;
  hubConnectionError: Error | undefined;
}

export const Overview = ({ monitors, hubConnection, hubConnectionError }: OverviewProps) => {
  const allServicesCount = monitors.size;

  let firstUpdateAt = 0;
  let lastUpdateAt = 0;
  let onlineServicesCount = 0;
  let eventCount = 0;
  let totalAvailability = 0;
  for (const [, mp] of monitors) {
    const isOnline = mp.stats?.lastState == MonitorState.Up || mp.stats?.lastState == MonitorState.Warn;

    onlineServicesCount += isOnline ? 1 : 0;
    eventCount += mp.events.length;
    totalAvailability += ((mp.stats?.availability ?? 0) * mp.events.length) / 100;

    if (firstUpdateAt === 0 || firstUpdateAt > (mp.events[0]?.at ?? 0)) firstUpdateAt = mp.events[0]?.at ?? 0;
    if (lastUpdateAt < (mp.stats?.lastUpdate ?? 0)) lastUpdateAt = mp.stats?.lastUpdate ?? 0;
  }
  totalAvailability = (100 * totalAvailability) / eventCount;

  return (
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
        <Box filter="auto" blur={firstUpdateAt === 0 ? "6px" : "0px"}>
          <StatHelpText>From {dateTimeFromNow(firstUpdateAt, true)}</StatHelpText>
        </Box>
      </Stat>

      <Stat>
        <StatLabel>Updated</StatLabel>
        <Box filter="auto" blur={lastUpdateAt === 0 ? "6px" : "0px"}>
          <Tooltip hasArrow label={dateTimeToString(lastUpdateAt)} placement="left">
            <StatNumber noOfLines={1}>{dateTimeFromNow(lastUpdateAt)}</StatNumber>
          </Tooltip>
        </Box>
        <Tooltip hasArrow label={hubConnectionError?.message} isDisabled={hubConnectionError?.message === undefined} placement="left">
          <StatHelpText>
            <StatArrow type={hubConnection?.state === HubConnectionState.Connected ? "increase" : "decrease"} />
            {hubConnection?.state ?? HubConnectionState.Disconnected}
          </StatHelpText>
        </Tooltip>
      </Stat>
    </StatGroup>
  );
};
