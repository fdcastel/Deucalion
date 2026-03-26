import { Overview, MonitorList } from "./main";

import { logger } from "../services";

import { preloadConfiguration, useConfiguration } from "../contexts/ConfigurationContext";
import { preloadMonitors, useMonitors } from "../contexts/MonitorsContext";
import { useMonitorHubContext } from "../contexts/MonitorHubContext";

preloadConfiguration();
preloadMonitors();

// Log version information to console.
const buildInfo = { version: import.meta.env.VITE_BUILD_VERSION, build: import.meta.env.VITE_INFORMATIONAL_VERSION };
console.log(`Deucalion UI started.`, buildInfo.version ? buildInfo : "(build information not available)");

if (import.meta.env.PROD) {
  // Disable further logger messages when in production.
  logger.disableLogger();
}

export const App = () => {
  const { configurationData } = useConfiguration();
  const { monitorsData, groupedMonitorsData, usingImages } = useMonitors();
  const { isConnected, isConnecting, connectionError } = useMonitorHubContext();

  // Don't render until initial data is loaded (avoids flash of default values).
  if (!configurationData || !monitorsData) return null;

  return (
    <main className="container mx-auto max-w-6xl flex-grow p-2">
      <Overview
        title={configurationData.pageTitle ?? "Deucalion Status"}
        monitors={monitorsData}
        isConnected={isConnected}
        isConnecting={isConnecting}
        connectionError={connectionError}
      />
      <MonitorList groupedMonitors={groupedMonitorsData} usingImages={usingImages} />
    </main>
  );
};
