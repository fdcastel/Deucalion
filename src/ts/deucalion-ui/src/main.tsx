import React from "react";
import ReactDOM from "react-dom/client";

import { HeroUIProvider } from "@heroui/system";
import { ToastProvider } from "@heroui/toast";

import { App } from "./components/app";
import { ErrorBoundary } from "./components/error-boundary";

import { ConfigurationProvider } from "./contexts/ConfigurationContext";
import { MonitorsProvider } from "./contexts/MonitorsContext";
import { MonitorHubProvider } from "./contexts/MonitorHubContext";

import "./index.css";

const container = document.getElementById("root");
if (!container) throw new Error("Failed to find the root element");

ReactDOM.createRoot(container).render(
  <React.StrictMode>
    <HeroUIProvider>
      <ToastProvider />
      <ErrorBoundary>
        <React.Suspense fallback={null}>
          <ConfigurationProvider>
            <MonitorsProvider>
              <MonitorHubProvider>
                <App />
              </MonitorHubProvider>
            </MonitorsProvider>
          </ConfigurationProvider>
        </React.Suspense>
      </ErrorBoundary>
    </HeroUIProvider>
  </React.StrictMode>
);
