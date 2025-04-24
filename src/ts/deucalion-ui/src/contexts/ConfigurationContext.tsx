import React, { createContext, useContext } from "react";
import useSWR, { preload } from "swr";

import { DeucalionOptions } from "../services";
import { API_CONFIGURATION_URL, SWR_OPTIONS } from "../configuration";

// Fetcher function specific to configuration
export const configurationFetcher = (url: string) =>
  fetch(url)
    .then((response) => response.json())
    .then((json) => json as DeucalionOptions | undefined)
    .catch(() => undefined);

// Interface for the Configuration Context
interface IConfigurationContext {
  configurationData: DeucalionOptions | undefined;
  isConfigurationLoading: boolean;
  configurationError: Error | null;
}

// Create the context
const ConfigurationContext = createContext<IConfigurationContext | undefined>(undefined);

// Create the provider component
export const ConfigurationProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const configurationResponse = useSWR<DeucalionOptions | undefined>(API_CONFIGURATION_URL, configurationFetcher, SWR_OPTIONS);

  const value: IConfigurationContext = {
    configurationData: configurationResponse.data,
    isConfigurationLoading: configurationResponse.isValidating,
    configurationError: configurationResponse.error instanceof Error ? configurationResponse.error : null,
  };

  return <ConfigurationContext.Provider value={value}>{children}</ConfigurationContext.Provider>;
};

// Create the custom hook to consume the context
export const useConfiguration = () => {
  const context = useContext(ConfigurationContext);
  if (context === undefined) {
    throw new Error("useConfiguration must be used within a ConfigurationProvider");
  }
  return context;
};

// Preload function for configuration
export const preloadConfiguration = () => {
  void preload(API_CONFIGURATION_URL, configurationFetcher);
};
