import React from "react";
import { MonitorProps } from "../../services";
import { MonitorComponent } from "./monitor-component";

interface MonitorListProps {
  groupedMonitors: Map<string, MonitorProps[]> | undefined;
  usingImages: boolean;
}

export const MonitorList: React.FC<MonitorListProps> = ({ groupedMonitors, usingImages }) => {
  if (!groupedMonitors) return null;
  return (
    <div>
      {Array.from(groupedMonitors).map(([groupName, monitorsInGroup]) => (
        <div key={groupName} className="mb-4">
          {groupName && <div className="mb-2 text-2xl font-light md:text-3xl">{groupName}</div>}
          {monitorsInGroup.map((monitorProps) => (
            <MonitorComponent key={monitorProps.name} monitor={monitorProps} usingImages={usingImages} />
          ))}
        </div>
      ))}
    </div>
  );
};
