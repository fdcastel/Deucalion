import { useEffect, useState } from "react";

import { HubConnection, HubConnectionState, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

import {
  Center,
  Container,
  Flex,
  Image,
  List,
  ListItem,
  Skeleton,
  Spacer,
  Spinner,
  Stat,
  StatArrow,
  StatGroup,
  StatHelpText,
  StatLabel,
  StatNumber,
  Text,
  Tooltip,
  useToast,
} from "@chakra-ui/react";

import dayjs from "dayjs";
import utc from "dayjs/plugin/utc";
import duration from "dayjs/plugin/duration";
import relativeTime from "dayjs/plugin/relativeTime";

import { MonitorState, MonitorEventDto, MonitorChangedDto } from "./server-types";
import { MonitorProps, MonitorComponent } from "./components/MonitorComponent";
import { ThemeSwitcherComponent } from "./components/ThemeSwitcherComponent";

import * as logger from "./logger";

// --- Configuration

dayjs.extend(utc);
dayjs.extend(duration);
dayjs.extend(relativeTime);

if (import.meta.env.PROD) {
  // Disables logger in production mode.
  logger.disableLogger();
}

const DEUCALION_PAGE_TITLE = import.meta.env.DEUCALION_PAGE_TITLE as string
const DEUCALION_ROOT_URL = import.meta.env.DEUCALION_ROOT_URL as string

// --- App functions

const addStats = (m: MonitorProps) => {
  if (m.events.length > 0) {
    const unknownEventCount = m.events.reduce((acc, e) => acc + (e.st == MonitorState.Unknown ? 1 : 0), 0);
    const downEventCount = m.events.reduce((acc, e) => acc + (e.st == MonitorState.Down ? 1 : 0), 0);

    m.stats = {
      availability: (100 * (m.events.length - downEventCount)) / (m.events.length - unknownEventCount),
      lastState: m.events[m.events.length - 1].st,
      lastUpdate: m.events[m.events.length - 1].at,
    };
  }

  return m;
};

const monitorStateToToastStatus = (state: MonitorState) => {
  switch (state) {
    case MonitorState.Up:
      return "success";
    case MonitorState.Warn:
      return "warning";
    case MonitorState.Down:
      return "error";
    default:
      return "info";
  }
};

const monitorStateToToastDescription = (state: MonitorState) => {
  switch (state) {
    case MonitorState.Up:
      return "Is online.";
    case MonitorState.Warn:
      return "Changed to warning.";
    case MonitorState.Down:
      return "Is down.";
    default:
      return "---";
  }
};

// --- App

const DEGRADED_COLOR = "red.500";

const EMPTY_MONITORS = new Map<string, MonitorProps>();

const API_URL = new URL("/api/monitors/*", DEUCALION_ROOT_URL);
const HUB_URL = new URL("/hub/monitors", DEUCALION_ROOT_URL);

export const App = () => {
  const [allMonitors, setAllMonitors] = useState(EMPTY_MONITORS);

  const [hubConnection, setHubConnection] = useState<HubConnection>();
  const [hubConnectionErrorMessage, setHubConnectionErrorMessage] = useState<undefined | string>(undefined);
  const [hubConnectionRetries, setHubConnectionRetries] = useState(0);

  const toast = useToast();

  useEffect(() => {
    logger.log("Retry requested", hubConnectionRetries);

    if (allMonitors.size === 0) {
      // Fetch initial data only once (when allMonitors is empty).

      fetch(API_URL)
        .then((response) => response.json())
        .then((json) => {
          const initialData = json as MonitorProps[];
          const initialMonitors = json ? new Map(initialData.map((x: MonitorProps) => [x.name, addStats(x)])) : EMPTY_MONITORS;
          setAllMonitors(initialMonitors);
        })
        .catch(() => {
          // Initial fetch failed.
          setAllMonitors(EMPTY_MONITORS);
        });
    }

    const newConnection = new HubConnectionBuilder().withUrl(HUB_URL.toString()).configureLogging(LogLevel.Information).build();
    setHubConnection(newConnection);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- Reason: hubConnectionRetries should be the only dependency here.
  }, [hubConnectionRetries]);

  useEffect(() => {
    logger.log("hubConnection changed", hubConnection);

    document.title = DEUCALION_PAGE_TITLE;

    if (hubConnection && hubConnection.state === HubConnectionState.Disconnected) {
      logger.log("hubConnection is starting...", hubConnection);
      hubConnection
        .start()
        .then(() => {
          // Connection established. Install event handlers.

          hubConnection.on("MonitorChecked", (newEvent: MonitorEventDto) => {
            logger.log("[onMonitorChecked] e=", newEvent);

            const monitorName = newEvent.n ?? "";
            setAllMonitors((oldMonitors) => {
              const oldMonitorProps = oldMonitors.get(monitorName);
              if (oldMonitorProps) {
                const existingEvents = oldMonitorProps.events.filter((x) => x.at === newEvent.at);
                if (existingEvents.length === 0) {
                  const newMonitors = new Map(oldMonitors);

                  const newMonitorProps = addStats({
                    name: monitorName,
                    events: [...oldMonitorProps.events, newEvent].slice(-60), // keep only the last 60
                  });

                  newMonitors.set(monitorName, newMonitorProps);

                  return newMonitors;
                }
              }
              return oldMonitors;
            });
          });

          hubConnection.on("MonitorChanged", (e: MonitorChangedDto) => {
            logger.log("[MonitorChanged]", e);

            toast({
              title: e.n,
              description: monitorStateToToastDescription(e.st),
              status: monitorStateToToastStatus(e.st),
              position: "bottom-right",
              variant: "left-accent",
              isClosable: true,
            });
          });

          hubConnection.onclose((err) => {
            // Do not clear last error message if onClose does not provide one.
            if (err?.message) {
              setHubConnectionErrorMessage(err.message);
            }

            logger.warn("Hub connection closed. Retrying in 5s...");
            setTimeout(() => {
              setHubConnectionRetries((old) => old + 1);
            }, 5000);
          });

          // Clear last error message.
          setHubConnectionErrorMessage(undefined);
        })
        .catch((err: Error) => {
          // Initial connection failed.
          setHubConnectionErrorMessage(err.message);

          logger.warn("Hub connection failed. Retrying in 5s...");
          setTimeout(() => {
            setHubConnectionRetries((old) => old + 1);
          }, 5000);
        });
    }

    const cleanUp = () => {
      logger.warn("Cleanup called!");

      // Do NOT call hubConnection.stop() here.
      // It will cause a continuous cycle of connect/disconnects.
    };

    return () => {
      cleanUp();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hubConnection]);

  const allServicesCount = allMonitors.size;

  let lastUpdateAt = 0;
  let onlineServicesCount = 0;
  let totalAvailability = 0;
  for (const [, mp] of allMonitors) {
    const isOnline = mp.stats?.lastState == MonitorState.Up || mp.stats?.lastState == MonitorState.Warn;

    onlineServicesCount += isOnline ? 1 : 0;
    totalAvailability += isOnline ? mp.stats?.availability ?? 0 : 0;

    if (lastUpdateAt < (mp.stats?.lastUpdate ?? 0)) lastUpdateAt = mp.stats?.lastUpdate ?? 0;
  }
  totalAvailability /= allServicesCount;

  return (
    <Container padding="4" maxWidth="80em">
      <Flex>
        <Image src="/deucalion-icon.svg" height="3em" marginRight="1em" />
        <Text fontSize="3xl">{DEUCALION_PAGE_TITLE}</Text>
        <Spacer />
        <ThemeSwitcherComponent />
      </Flex>

      <StatGroup marginY="1em" padding="0.5em" bg="blackAlpha.200" boxShadow="md" borderRadius="md">
        <Stat>
          <StatLabel>Services</StatLabel>
          <Skeleton isLoaded={allServicesCount > 0} width="50%">
            <StatNumber>
              {onlineServicesCount} of {allServicesCount}
            </StatNumber>
          </Skeleton>

          {onlineServicesCount === allServicesCount ? (
            <StatHelpText>Online</StatHelpText>
          ) : (
            <StatHelpText color={DEGRADED_COLOR}>Degraded</StatHelpText>
          )}
        </Stat>

        <Stat>
          <StatLabel>Availability</StatLabel>
          <Skeleton isLoaded={allServicesCount > 0} width="50%">
            <StatNumber>{totalAvailability.toFixed(1)}%</StatNumber>
          </Skeleton>
          <StatHelpText>Last hour</StatHelpText>
        </Stat>

        <Stat>
          <StatLabel>Updated</StatLabel>
          <Skeleton isLoaded={allServicesCount > 0} width="100%">
            <StatNumber>{dayjs.unix(lastUpdateAt).fromNow()}</StatNumber>
          </Skeleton>
          <Tooltip hasArrow label={hubConnectionErrorMessage} isDisabled={hubConnectionErrorMessage === undefined} placement="left">
            <StatHelpText>
              <StatArrow type={hubConnection?.state === HubConnectionState.Connected ? "increase" : "decrease"} />
              {hubConnection?.state ?? HubConnectionState.Disconnected}
            </StatHelpText>
          </Tooltip>
        </Stat>
      </StatGroup>

      <List spacing="1em" padding="0.5em" bg="blackAlpha.100" boxShadow="md" borderRadius="md">
        {allServicesCount > 0 ? (
          Array.from(allMonitors).map(([monitorName, monitorProps]) => (
            <ListItem key={monitorName}>
              <MonitorComponent name={monitorName} events={monitorProps.events} stats={monitorProps.stats} />
            </ListItem>
          ))
        ) : (
          <Center>
            <Spinner color="gray.600" emptyColor="gray.400" size="lg" />
          </Center>
        )}
      </List>
    </Container>
  );
};
