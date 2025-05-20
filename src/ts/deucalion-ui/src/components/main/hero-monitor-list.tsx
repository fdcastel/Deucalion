import React from "react";
import { MonitorProps } from "../../services";
import { HeroMonitorComponent } from "./hero-monitor-component";

interface HeroMonitorListProps {
  groupedMonitors: Map<string, MonitorProps[]> | undefined;
  usingImages: boolean;
}

export const HeroMonitorList: React.FC<HeroMonitorListProps> = ({ groupedMonitors, usingImages }) => {
  if (!groupedMonitors) return null;
  return (
    <div>
      {Array.from(groupedMonitors).map(([groupName, monitorsInGroup]) => (
        <div key={groupName} className="mb-4 bg-white/80 rounded-lg shadow">
          {groupName && (
            <div className="py-2 px-4 border-b border-gray-200 text-lg font-light">{groupName}</div>
          )}
          <div className="py-2 px-4">
            {monitorsInGroup.map((monitorProps) => (
              <HeroMonitorComponent key={monitorProps.name} monitor={monitorProps} usingImages={usingImages} />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
};
