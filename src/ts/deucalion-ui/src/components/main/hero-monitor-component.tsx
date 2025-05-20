import React, { useState, useEffect } from "react";
import { MonitorState, MonitorProps } from "../../services";
import { formatLastSeen, formatMonitorEventHero, monitorStateToHeroColor } from "../../services";
import { Tooltip } from "@heroui/react";

interface HeroMonitorComponentProps {
  monitor: MonitorProps;
  usingImages?: boolean;
}

export const HeroMonitorComponent: React.FC<HeroMonitorComponentProps> = ({ monitor, usingImages }) => {
  const { name, config, stats, events } = monitor;
  const lastState = stats?.lastState ?? MonitorState.Unknown;
  const [isFlashing, setIsFlashing] = useState(false);

  useEffect(() => {
    if (events.length > 0) {
      setIsFlashing(true);
      const timer = setTimeout(() => setIsFlashing(false), 500);
      return () => clearTimeout(timer);
    }
  }, [events]);

  const lastSeenAt = formatLastSeen(lastState, monitor.stats);

  return (
    // For a compact view, remove "p-1":
    <div className={`flex items-center transition-colors duration-500 ${isFlashing ? "bg-flash-light dark:bg-flash-dark" : ""} rounded-md p-1 px-2`}>
      {usingImages && (
        <span className="hidden md:inline-block">
          {config.image ? <img src={config.image} className="mr-2 h-8 w-8 min-w-8" alt="icon" /> : <span className="mr-2 inline-block h-8 w-8" />}
        </span>
      )}
      <Tooltip content={lastSeenAt} showArrow={true} isDisabled={!lastSeenAt} placement="bottom-start">
        <span className={`min-w-[6em] truncate md:min-w-[8em] ${lastState !== MonitorState.Up ? `text-${monitorStateToHeroColor(lastState)}` : ""}`}>
          {config.href ? (
            <a
              href={config.href}
              target="_blank"
              rel="noopener noreferrer"
              className={lastState !== MonitorState.Up ? `text-${monitorStateToHeroColor(lastState)}` : undefined}
            >
              {name}
            </a>
          ) : (
            name
          )}
        </span>
      </Tooltip>
      <span className="flex-1" />
      <div className="flex items-center overflow-x-hidden">
        <div className="mr-1 flex h-10 flex-row-reverse items-center justify-start overflow-x-hidden">
          {events.map((e) => (
            <Tooltip key={e.at} content={formatMonitorEventHero(e)} showArrow={true} placement="bottom">
              <span
                className={`mr-1 inline-block h-6 min-w-[0.5em] rounded-xl bg-${monitorStateToHeroColor(e.st)} transition-transform duration-200 hover:-translate-y-1`}
              />
            </Tooltip>
          ))}
        </div>
        <span className="hidden md:inline-block">
          <Tooltip content="Availability" showArrow={true}>
            <span className="mr-1 inline-block min-w-[4em] rounded-full bg-teal-500 text-center text-white">
              {stats?.availability !== undefined ? stats.availability.toFixed(0) : "... "}%
            </span>
          </Tooltip>
        </span>
        <Tooltip content="Average response time" showArrow={true}>
          <span className="inline-block min-w-[5em] bg-cyan-500 text-center text-white">
            {stats?.averageResponseTimeMs !== undefined ? stats.averageResponseTimeMs.toFixed(0) : "... "}ms
          </span>
        </Tooltip>
      </div>
    </div>
  );
};
