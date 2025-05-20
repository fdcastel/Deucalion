import React from "react";
import ReactDOM from "react-dom/client";

import {HeroUIProvider, ToastProvider} from '@heroui/react'

import { ChakraEnvironment } from "./chakra-environment";
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
      <ToastProvider />
      <ChakraEnvironment>
        <ConfigurationProvider>
          <MonitorsProvider>
            <MonitorHubProvider>
              <App />
            </MonitorHubProvider>
          </MonitorsProvider>
        </ConfigurationProvider>
      </ChakraEnvironment>
    </HeroUIProvider>    
  </React.StrictMode>
);

