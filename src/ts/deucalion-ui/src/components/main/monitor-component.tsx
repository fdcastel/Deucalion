import React, { useState, useLayoutEffect, useMemo, useRef } from "react";
import { MonitorState, MonitorProps } from "../../services";
import { formatLastSeen, formatMonitorEvent, monitorStateToColor } from "../../services";
import { Tooltip, Chip } from "@heroui/react";
import { useMediaQuery } from "../../hooks/useMediaQuery";

interface MonitorComponentProps {
  monitor: MonitorProps;
  usingImages?: boolean;
}

export const MonitorComponent: React.FC<MonitorComponentProps> = ({ monitor, usingImages }) => {
  const { name, config, stats, events } = monitor;
  const lastState = stats?.lastState ?? MonitorState.Unknown;
  const [isFlashing, setIsFlashing] = useState(false);
  const flashTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const latestEventAt = useMemo(() => (events.length > 0 ? events[0].at : null), [events]);
  const previousEventAtRef = useRef<number | null>(latestEventAt);

  useLayoutEffect(() => {
    if (!latestEventAt) {
      previousEventAtRef.current = latestEventAt;
      return;
    }

    if (latestEventAt !== previousEventAtRef.current) {
      if (flashTimeoutRef.current) {
        clearTimeout(flashTimeoutRef.current);
      }

      setIsFlashing(true);
      flashTimeoutRef.current = setTimeout(() => {
        setIsFlashing(false);
        flashTimeoutRef.current = null;
      }, 500);
    }

    previousEventAtRef.current = latestEventAt;

    return () => {
      if (flashTimeoutRef.current) {
        clearTimeout(flashTimeoutRef.current);
        flashTimeoutRef.current = null;
      }
    };
  }, [latestEventAt]);

  const isMdScreen = useMediaQuery("(min-width: 768px)");
  const lastSeenAt = formatLastSeen(lastState, monitor.stats);

  return (
    <div
      className={`flex items-center transition-colors ${isFlashing ? "duration-75 bg-flash-light dark:bg-flash-dark" : "duration-500"} my-1 h-10 rounded-md px-2`}
    >
      {usingImages && (
        <span className="hidden md:inline-block">
          {config.image ? <img src={config.image} className="icon-size-8 mr-2 min-w-8" alt="icon" /> : <span className="icon-size-8 mr-2 inline-block" />}
        </span>
      )}
      <div className="flex w-[7.5rem] min-w-0 items-center gap-2 sm:w-[9rem] md:w-[11rem] lg:w-[14rem]">
        <Tooltip delay={0} isDisabled={!lastSeenAt}>
          <Tooltip.Trigger>
            {config.href ? (
              <a
                href={config.href}
                target="_blank"
                rel="noopener noreferrer"
                className={`truncate ${lastState !== MonitorState.Up ? `text-${monitorStateToColor(lastState)}` : ""}`}
              >
                {name}
              </a>
            ) : (
              <button type="button" className={`truncate text-left ${lastState !== MonitorState.Up ? `text-${monitorStateToColor(lastState)}` : ""}`}>
                {name}
              </button>
            )}
          </Tooltip.Trigger>
          <Tooltip.Content showArrow placement="bottom start">
            {lastSeenAt}
          </Tooltip.Content>
        </Tooltip>
        {config.tags && config.tags.length > 0 && (
          <div className="hidden gap-1 lg:flex">
            {config.tags.map((tag) => (
              <Chip key={tag} size="sm" variant="tertiary" className="h-5 text-xs">
                {tag}
              </Chip>
            ))}
          </div>
        )}
      </div>
      <div className="mr-1 flex min-w-0 flex-1 items-center overflow-x-hidden">
        <div className="flex min-w-0 flex-1 flex-row-reverse items-center justify-start overflow-x-hidden">
          {events.map((e) => (
            <Tooltip key={e.at} delay={0}>
              <Tooltip.Trigger>
                <button
                  type="button"
                  className={`mr-1 inline-block h-6 min-w-[0.5em] rounded-xl bg-${monitorStateToColor(e.st)} transition-transform duration-200 hover:-translate-y-1`}
                />
              </Tooltip.Trigger>
              <Tooltip.Content showArrow placement="bottom">
                {formatMonitorEvent(e)}
              </Tooltip.Content>
            </Tooltip>
          ))}
        </div>
        {isMdScreen && (
          <Tooltip delay={0}>
            <Tooltip.Trigger>
              <button type="button" className="mr-1 inline-block min-w-[4em] rounded-full bg-teal-500 text-center text-white">
                {stats?.availability !== undefined ? stats.availability.toFixed(0) : "... "}%
              </button>
            </Tooltip.Trigger>
            <Tooltip.Content showArrow>
              Availability
            </Tooltip.Content>
          </Tooltip>
        )}
        <Tooltip delay={0}>
          <Tooltip.Trigger>
            <button type="button" className="inline-block min-w-[5em] bg-cyan-500 text-center text-white">
              {stats?.averageResponseTimeMs !== undefined ? stats.averageResponseTimeMs.toFixed(0) : "... "}ms
            </button>
          </Tooltip.Trigger>
          <Tooltip.Content showArrow>
            Average response time
          </Tooltip.Content>
        </Tooltip>
      </div>
    </div>
  );
};
