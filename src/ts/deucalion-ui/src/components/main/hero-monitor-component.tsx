import React, { useState, useEffect } from "react";
import { MonitorState, MonitorProps } from "../../services";
import { formatLastSeen, formatMonitorEvent, monitorStateToHeroColor } from "../../services";

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

  return (
    <div
      className={`flex items-center transition-colors duration-500 ${isFlashing ? 'bg-yellow-100' : ''} p-1 rounded-md`}
    >
      {usingImages && (
        <span className="hidden md:inline-block">
          {config.image ? (
            <img src={config.image} className="w-8 h-8 mr-2 min-w-8" alt="icon" />
          ) : (
            <span className="w-8 h-8 mr-2 inline-block" />
          )}
        </span>
      )}
      <span
        className={`truncate min-w-[6em] md:min-w-[8em] ${lastState !== MonitorState.Up ? `text-${monitorStateToHeroColor(lastState)}` : ""}`}
        title={formatLastSeen(lastState, monitor.stats) || undefined}
      >
        {config.href ? (
          <a href={config.href} target="_blank" rel="noopener noreferrer" className={lastState !== MonitorState.Up ? `text-${monitorStateToHeroColor(lastState)}` : undefined}>{name}</a>
        ) : (
          name
        )}
      </span>
      <span className="flex-1" />
      <div className="flex items-center overflow-x-hidden">
        <div className="flex items-center h-10 overflow-x-hidden flex-row-reverse justify-start mr-2">
          {events.map((e) => (
            <span
              key={e.at}
              title={formatMonitorEvent(e)}
              className={`inline-block rounded-xl h-6 min-w-[0.5em] mr-1 bg-${monitorStateToHeroColor(e.st)}`}
            />
          ))}
        </div>
        <span className="hidden md:inline-block">
          <span title="Availability" className="ml-1 min-w-[4em] inline-block bg-teal-500 text-white rounded-full text-center px-2">
            {stats?.availability !== undefined ? stats.availability.toFixed(0) : "... "}%
          </span>
        </span>
        <span title="Average response time" className="ml-1 min-w-[5em] inline-block bg-cyan-500 text-white text-center px-2">
          {stats?.averageResponseTimeMs !== undefined ? stats.averageResponseTimeMs.toFixed(0) : "... "}ms
        </span>
      </div>
    </div>
  );
};
