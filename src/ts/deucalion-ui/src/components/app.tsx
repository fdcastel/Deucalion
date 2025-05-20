import { EMPTY_MONITORS } from "../services";
import { HeroOverview, HeroMonitorList } from "./main";

import { logger } from "../services";

import { preloadConfiguration, useConfiguration } from "../contexts/ConfigurationContext";
import { preloadMonitors, useMonitors } from "../contexts/MonitorsContext";
import { useMonitorHubContext } from "../contexts/MonitorHubContext";

preloadConfiguration();
preloadMonitors();

if (import.meta.env.PROD) {
  logger.disableLogger();
}

export const App = () => {
  const { configurationData } = useConfiguration();
  const { monitorsData, groupedMonitorsData, usingImages } = useMonitors();
  const { isConnected, isConnecting, connectionError } = useMonitorHubContext();

  return (
    <>
      {/* Hero UI area */}
      <div className="relative flex h-screen flex-col">
        <main className="container mx-auto max-w-7xl flex-grow px-2 pt-16">
          <HeroOverview
            title={configurationData?.pageTitle ?? "Deucalion Status"}
            monitors={monitorsData ?? EMPTY_MONITORS}
            isConnected={isConnected}
            isConnecting={isConnecting}
            connectionError={connectionError}
          />
          <HeroMonitorList groupedMonitors={groupedMonitorsData} usingImages={usingImages} />
        </main>
      </div>
    </>
  );
};
