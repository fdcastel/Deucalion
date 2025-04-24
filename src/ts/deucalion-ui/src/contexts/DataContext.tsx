import React, { createContext, useContext, useMemo } from "react";
import useSWR, { preload, MutatorCallback, MutatorOptions } from "swr";

import { DeucalionOptions, MonitorProps } from "../services";
import { API_CONFIGURATION_URL, API_MONITORS_URL } from "../configuration";

export const configurationFetcher = (url: string) =>
  fetch(url)
    .then((response) => response.json())
    .then((json) => json as DeucalionOptions | undefined)
    .catch(() => undefined);

export const monitorsFetcher = (url: string) =>
  fetch(url)
    .then((response) => response.json())
    .then((json) => json as MonitorProps[] | undefined)
    .then((arr) => (arr ? new Map(arr.map((x) => [x.name, x])) : undefined))
    .catch(() => undefined);

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

interface IDataContext {
  configurationData: DeucalionOptions | undefined;
  monitorsData: Map<string, MonitorProps> | undefined; // Keep raw data for Overview calculations
  groupedMonitorsData: Map<string, MonitorProps[]> | undefined; // Add grouped data
  usingImages: boolean; // Add flag for image usage

  isConfigurationLoading: boolean;
  isMonitorsLoading: boolean;

  configurationError: Error | undefined;
  monitorsError: Error | undefined;

  mutateMonitors: (
    data?: Map<string, MonitorProps> | Promise<Map<string, MonitorProps> | undefined> | MutatorCallback<Map<string, MonitorProps> | undefined>,
    opts?: boolean | MutatorOptions<Map<string, MonitorProps> | undefined>
  ) => Promise<Map<string, MonitorProps> | undefined>;
}

// Create the context with an undefined initial value
const DataContext = createContext<IDataContext | undefined>(undefined);

// Create the provider component
export const DataProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const SWR_OPTIONS = { suspense: true, revalidateOnMount: false, revalidateIfStale: false, revalidateOnFocus: false, revalidateOnReconnect: false };

  const configurationResponse = useSWR<DeucalionOptions | undefined>(API_CONFIGURATION_URL, configurationFetcher, SWR_OPTIONS);
  const monitorsResponse = useSWR<Map<string, MonitorProps> | undefined>(API_MONITORS_URL, monitorsFetcher, SWR_OPTIONS);

  const groupedMonitors = useMemo(() => groupMonitors(monitorsResponse.data), [monitorsResponse.data]);

  // Determine if any monitor uses images
  const usingImages = useMemo(() => {
    if (!monitorsResponse.data) return false;
    return Array.from(monitorsResponse.data.values()).some(mp => mp.config.image);
  }, [monitorsResponse.data]);

  const value: IDataContext = {
    configurationData: configurationResponse.data,
    monitorsData: monitorsResponse.data,
    groupedMonitorsData: groupedMonitors,
    usingImages: usingImages,
    isConfigurationLoading: configurationResponse.isValidating,
    isMonitorsLoading: monitorsResponse.isValidating,
    configurationError: configurationResponse.error as Error | undefined,
    monitorsError: monitorsResponse.error as Error | undefined,
    mutateMonitors: monitorsResponse.mutate,
  };

  // Provide the context value to children components
  return <DataContext.Provider value={value}>{children}</DataContext.Provider>;
};

// Create the custom hook to consume the context
export const useData = () => {
  const context = useContext(DataContext);

  if (context === undefined) {
    throw new Error("useData must be used within a DataProvider");
  }

  return context;
};

// Preload function
export const preloadData = () => {
  void preload(API_CONFIGURATION_URL, configurationFetcher);
  void preload(API_MONITORS_URL, monitorsFetcher);
};
