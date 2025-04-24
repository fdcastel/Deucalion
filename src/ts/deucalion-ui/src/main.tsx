import React from "react";
import ReactDOM from "react-dom/client";

import { ChakraEnvironment } from "./chakra-environment";
import { App } from "./components/app";
import { DataProvider } from "./contexts/DataContext";
import { MonitorHubProvider } from "./contexts/MonitorHubContext";

const container = document.getElementById("root");
if (!container) throw new Error("Failed to find the root element");

ReactDOM.createRoot(container).render(
  <React.StrictMode>
    <ChakraEnvironment>
      <DataProvider>
        <MonitorHubProvider>
          <App />
        </MonitorHubProvider>
      </DataProvider>
    </ChakraEnvironment>
  </React.StrictMode>
);

