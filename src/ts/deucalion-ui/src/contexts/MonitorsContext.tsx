import React, { createContext, useContext, useMemo } from "react";
import useSWR, { preload, MutatorCallback, MutatorOptions } from "swr";

import { MonitorProps } from "../services";
import { API_MONITORS_URL, SWR_OPTIONS } from "../configuration";

// Fetcher function specific to monitors
export const monitorsFetcher = (url: string) =>
  fetch(url)
    .then((response) => response.json())
    .then((json) => json as MonitorProps[] | undefined)
    .then((arr) => (arr ? new Map(arr.map((x) => [x.name, x])) : undefined))
    .catch(() => undefined);

// Helper function to group monitors
const groupMonitors = (monitors: Map<string, MonitorProps> | undefined): Map<string, MonitorProps[]> => {
  const grouped = new Map<string, MonitorProps[]>();
  if (!monitors) {
    return grouped;
  }

  for (const [, monitorProps] of monitors) {
    const groupKey = monitorProps.config.group ?? ""; // Use empty string for default group
    let slot = grouped.get(groupKey);
    if (!slot) {
      slot = [];
      grouped.set(groupKey, slot);
    }
    slot.push(monitorProps);
  }
  return grouped;
};

// Interface for the Monitors Context
interface IMonitorsContext {
  monitorsData: Map<string, MonitorProps> | undefined;
  groupedMonitorsData: Map<string, MonitorProps[]> | undefined;
  usingImages: boolean;
  isMonitorsLoading: boolean;
  monitorsError: Error | null;
  mutateMonitors: (
    data?: Map<string, MonitorProps> | Promise<Map<string, MonitorProps> | undefined> | MutatorCallback<Map<string, MonitorProps> | undefined>,
    opts?: boolean | MutatorOptions<Map<string, MonitorProps> | undefined>
  ) => Promise<Map<string, MonitorProps> | undefined>;
}

// Create the context
const MonitorsContext = createContext<IMonitorsContext | undefined>(undefined);

// Create the provider component
export const MonitorsProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const monitorsResponse = useSWR<Map<string, MonitorProps> | undefined>(API_MONITORS_URL, monitorsFetcher, SWR_OPTIONS);

  const groupedMonitors = useMemo(() => groupMonitors(monitorsResponse.data), [monitorsResponse.data]);

  const usingImages = useMemo(() => {
    if (!monitorsResponse.data) return false;
    return Array.from(monitorsResponse.data.values()).some(mp => mp.config.image);
  }, [monitorsResponse.data]);

  const value: IMonitorsContext = {
    monitorsData: monitorsResponse.data,
    groupedMonitorsData: groupedMonitors,
    usingImages: usingImages,
    isMonitorsLoading: monitorsResponse.isValidating,
    monitorsError: monitorsResponse.error instanceof Error ? monitorsResponse.error : null,
    mutateMonitors: monitorsResponse.mutate,
  };

  return <MonitorsContext.Provider value={value}>{children}</MonitorsContext.Provider>;
};

// Create the custom hook to consume the context
export const useMonitors = () => {
  const context = useContext(MonitorsContext);
  if (context === undefined) {
    throw new Error("useMonitors must be used within a MonitorsProvider");
  }
  return context;
};

// Preload function for monitors
export const preloadMonitors = () => {
  void preload(API_MONITORS_URL, monitorsFetcher);
};
