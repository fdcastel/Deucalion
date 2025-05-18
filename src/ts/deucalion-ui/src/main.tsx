import React from "react";
import ReactDOM from "react-dom/client";

import { Provider } from "@/components/ui/provider"
import { Toaster } from "./components/ui/toaster";

import { App } from "./components/app";

import { ConfigurationProvider } from "./contexts/ConfigurationContext";
import { MonitorsProvider } from "./contexts/MonitorsContext";
import { MonitorHubProvider } from "./contexts/MonitorHubContext";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Provider>
      <ConfigurationProvider>
        <MonitorsProvider>
          <MonitorHubProvider>
            <App />
            <Toaster />
          </MonitorHubProvider>
        </MonitorsProvider>
      </ConfigurationProvider>
    </Provider>
  </React.StrictMode>,
)

