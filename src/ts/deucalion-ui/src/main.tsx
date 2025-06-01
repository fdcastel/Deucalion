import React from "react";
import ReactDOM from "react-dom/client";

import { HeroUIProvider, ToastProvider } from "@heroui/react";
import { ThemeProvider as NextThemesProvider } from "next-themes";

import { App } from "./components/app";

import { ConfigurationProvider } from "./contexts/ConfigurationContext";
import { MonitorsProvider } from "./contexts/MonitorsContext";
import { MonitorHubProvider } from "./contexts/MonitorHubContext";

import "./index.css";

const container = document.getElementById("root");
if (!container) throw new Error("Failed to find the root element");

ReactDOM.createRoot(container).render(
  <React.StrictMode>
    <HeroUIProvider>
      <NextThemesProvider attribute="class" enableSystem={true}>
        <ToastProvider />
        <ConfigurationProvider>
          <MonitorsProvider>
            <MonitorHubProvider>
              <App />
            </MonitorHubProvider>
          </MonitorsProvider>
        </ConfigurationProvider>
      </NextThemesProvider>
    </HeroUIProvider>
  </React.StrictMode>
);
