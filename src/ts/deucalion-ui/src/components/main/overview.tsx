import React, { useEffect } from "react";
import { MdArrowUpward, MdArrowDownward } from "react-icons/md";
import { Tooltip } from "@heroui/tooltip";
import { MonitorState, MonitorProps, dateTimeFromNow, dateTimeToString } from "../../services";
import deucalionIconDataUri from '/assets/deucalion-icon.svg?inline';

import { ThemeSwitcher } from "../ui/theme-switcher";
import { StatCard, StatCardFooter } from "../ui/stat-card";

interface OverviewProps {
  title: string;
  monitors: Map<string, MonitorProps>;
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: Error | null;
}

export const Overview: React.FC<OverviewProps> = ({ title, monitors, isConnected, isConnecting, connectionError }) => {
  const allServicesCount = monitors.size;

  let firstUpdateAt = Number.MAX_VALUE;
  let lastUpdateAt = 0;

  let onlineServicesCount = 0;
  let eventCount = 0;
  let totalAvailability = 0;
  let monitorsWithStats = 0;
  for (const [, mp] of monitors) {
    const isOnline = mp.stats?.lastState === MonitorState.Up || mp.stats?.lastState === MonitorState.Warn;
    onlineServicesCount += isOnline ? 1 : 0;
    eventCount += mp.events.length;
    if (mp.stats?.availability !== undefined) {
      totalAvailability += mp.stats.availability;
      monitorsWithStats++;
    }
    firstUpdateAt = mp.events[0]?.at ? Math.min(firstUpdateAt, mp.events[0].at) : firstUpdateAt;
    lastUpdateAt = mp.stats?.lastUpdate ? Math.max(lastUpdateAt, mp.stats.lastUpdate) : lastUpdateAt;
  }
  totalAvailability = monitorsWithStats > 0 ? totalAvailability / monitorsWithStats : 0;

  useEffect(() => {
    document.title = onlineServicesCount === allServicesCount ? title : `(-${String(allServicesCount - onlineServicesCount)}) ${title}`;
  }, [title, allServicesCount, onlineServicesCount]);

  const connectionStatusText = isConnected ? "Connected" : isConnecting ? "Connecting..." : "Disconnected";

  return (
    <div className="flex flex-col">
      <div className="mb-4 flex items-center">
        <img src={deucalionIconDataUri} className="icon-size-12 mr-2 app-icon-effect" alt="icon" />
        <span className="truncate text-3xl">{title}</span>
        <div className="flex-1" />
        <ThemeSwitcher />
      </div>
      <div className="mb-4 flex flex-wrap items-start justify-around rounded-md bg-black/10 p-2 shadow-lg">
        <StatCard title="Services">
          <span className="text-2xl font-semibold">
            {onlineServicesCount} of {allServicesCount}
          </span>
          <StatCardFooter>
            {allServicesCount > 0 && onlineServicesCount === allServicesCount ? (
              "Online"
            ) : allServicesCount > 0 && onlineServicesCount === 0 ? (
              <span className="text-monitor-down">All Offline</span>
            ) : allServicesCount > 0 ? (
              <span className="text-monitor-down">Degraded</span>
            ) : (
              "No services" // Unexpected state.
            )}
          </StatCardFooter>
        </StatCard>
        <StatCard title="Availability" blur={eventCount === 0}>
          {totalAvailability !== undefined ? <span className="text-2xl font-semibold">{totalAvailability.toFixed(1)}%</span> : null}
          <StatCardFooter blur={firstUpdateAt === Number.MAX_VALUE}>From {dateTimeFromNow(firstUpdateAt, true)}</StatCardFooter>
        </StatCard>
        <StatCard title="Updated" className="hidden md:block" blur={lastUpdateAt === 0}>
          <Tooltip content={dateTimeToString(lastUpdateAt)} showArrow={true} placement="left">
            <span className="text-2xl font-semibold">{dateTimeFromNow(lastUpdateAt)}</span>
          </Tooltip>
          <StatCardFooter>
            <Tooltip content={connectionError?.message} showArrow={true} isDisabled={!connectionError?.message} placement="bottom-end">
              <span className={isConnected ? "text-monitor-up" : "text-monitor-down"}>
                {isConnected ? (
                  <MdArrowUpward className="icon-size-5 mr-1 inline align-middle" />
                ) : (
                  <MdArrowDownward className="icon-size-5 mr-1 inline align-middle" />
                )}
                {connectionStatusText}
              </span>
            </Tooltip>
          </StatCardFooter>
        </StatCard>
      </div>
    </div>
  );
};
