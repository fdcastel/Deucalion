import React, { useEffect } from "react";
import { MonitorState, MonitorProps, dateTimeFromNow, dateTimeToString } from "../../services";
import { ThemeSwitcher } from "./theme-switcher";
import { Tooltip } from "@heroui/react";
import { MdArrowUpward, MdArrowDownward } from "react-icons/md";

interface HeroOverviewProps {
  title: string;
  monitors: Map<string, MonitorProps>;
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: Error | null;
}

export const HeroOverview: React.FC<HeroOverviewProps> = ({ title, monitors, isConnected, isConnecting, connectionError }) => {
  const allServicesCount = monitors.size;

  let firstUpdateAt = Number.MAX_VALUE;
  let lastUpdateAt = 0;

  let onlineServicesCount = 0;
  let eventCount = 0;
  let totalAvailability = 0;
  for (const [, mp] of monitors) {
    const isOnline = mp.stats?.lastState === MonitorState.Up || mp.stats?.lastState === MonitorState.Warn;
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
    <div className="flex flex-col">
      <div className="mb-4 flex items-center">
        <img src="/assets/deucalion-icon.svg" className="mr-2 h-12 w-12" alt="icon" />
        <span className="truncate text-3xl">{title}</span>
        <div className="flex-1" />
        <ThemeSwitcher />
      </div>
      <div className="mb-4 flex flex-wrap items-start justify-around rounded-md bg-black/10 p-2 shadow-lg">
        <div className="min-w-0 flex-1 basis-0">
          <div className="text-sm text-gray-500">Services</div>
          <div className={onlineServicesCount === 0 ? "blur-sm" : ""}>
            <span className="text-2xl font-semibold">
              {onlineServicesCount} of {allServicesCount}
            </span>
          </div>
          <div className="text-xs text-gray-500">
            {onlineServicesCount === 0 ? (
              "Loading..."
            ) : onlineServicesCount === allServicesCount ? (
              "Online"
            ) : (
              <span className="text-monitor-down">Degraded</span>
            )}
          </div>
        </div>
        <div className="min-w-0 flex-1 basis-0">
          <div className="text-sm text-gray-500">Availability</div>
          <div className={isNaN(totalAvailability) ? "blur-sm" : ""}>
            <span className="text-2xl font-semibold">{totalAvailability.toFixed(1)}%</span>
          </div>
          <div className={"text-xs " + (firstUpdateAt === Number.MAX_VALUE ? "blur-sm" : "")}>
            <span className="text-gray-500">From {dateTimeFromNow(firstUpdateAt, true)}</span>
          </div>
        </div>
        <div className="hidden flex-1 basis-0 md:block">
          <div className="text-sm text-gray-500">Updated</div>
          <div className={lastUpdateAt === 0 ? "blur-sm" : ""}>
            <Tooltip content={dateTimeToString(lastUpdateAt)} showArrow={true} placement="left">
              <span className="text-2xl font-semibold">{dateTimeFromNow(lastUpdateAt)}</span>
            </Tooltip>
          </div>
          <div className="text-xs text-gray-500">
            <Tooltip content={connectionError?.message} showArrow={true} isDisabled={!connectionError?.message} placement="bottom-end">
              <span className={isConnected ? "text-monitor-up" : "text-monitor-down"}>
                {isConnected ? <MdArrowUpward className="mr-1 inline align-middle" /> : <MdArrowDownward className="mr-1 inline align-middle" />}
                {connectionStatusText}
              </span>
            </Tooltip>
          </div>
        </div>
      </div>
    </div>
  );
};
