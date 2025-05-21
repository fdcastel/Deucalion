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
        <div key={groupName} className="mb-4">
          {groupName && <div className="mb-2 text-2xl font-light md:text-3xl">{groupName}</div>}
          {monitorsInGroup.map((monitorProps) => (
            <HeroMonitorComponent key={monitorProps.name} monitor={monitorProps} usingImages={usingImages} />
          ))}
        </div>
      ))}
    </div>
  );
};
