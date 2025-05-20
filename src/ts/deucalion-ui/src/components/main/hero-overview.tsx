import React, { useEffect } from "react";
import { MonitorState, MonitorProps, dateTimeFromNow, dateTimeToString } from "../../services";
import { ThemeSwitcher } from "./theme-switcher";

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
      <div className="flex items-center">
        <img src="/assets/deucalion-icon.svg" className="w-12 h-12 mr-2" alt="icon" />
        <span className="text-3xl truncate">{title}</span>
        <div className="flex-1" />
        <ThemeSwitcher />
      </div>
      <div className="flex gap-6 my-4 p-2 pb-0 bg-black/10 shadow rounded-md">
        <div>
          <div className="text-sm text-gray-600">Services</div>
          <div className={onlineServicesCount === 0 ? "blur-sm" : ""}>
            <span className="text-2xl font-bold">{onlineServicesCount} of {allServicesCount}</span>
          </div>
          <div className="text-xs text-gray-500">
            {onlineServicesCount === 0 ? "Loading..." : onlineServicesCount === allServicesCount ? "Online" : <span className="text-red-600">Degraded</span>}
          </div>
        </div>
        <div>
          <div className="text-sm text-gray-600">Availability</div>
          <div className={isNaN(totalAvailability) ? "blur-sm" : ""}>
            <span className="text-2xl font-bold">{totalAvailability.toFixed(1)}%</span>
          </div>
          <div className={firstUpdateAt === Number.MAX_VALUE ? "blur-sm" : ""}>
            <span className="text-xs text-gray-500">From {dateTimeFromNow(firstUpdateAt, true)}</span>
          </div>
        </div>
        <div className="hidden md:block">
          <div className="text-sm text-gray-600">Updated</div>
          <div className={lastUpdateAt === 0 ? "blur-sm" : ""}>
            <span className="text-2xl font-bold" title={dateTimeToString(lastUpdateAt)}>{dateTimeFromNow(lastUpdateAt)}</span>
          </div>
          <div className="text-xs text-gray-500" title={connectionError?.message ?? undefined}>
            <span className={isConnected ? "text-green-600" : "text-red-600"}>{connectionStatusText}</span>
          </div>
        </div>
      </div>
    </div>
  );
};
