import React from "react";
import ReactDOM from "react-dom/client";

import { ChakraEnvironment } from "./chakra-environment";
import { App } from "./components/app";

const container = document.getElementById("root");
if (!container) throw new Error("Failed to find the root element");

ReactDOM.createRoot(container).render(
  <React.StrictMode>
    <ChakraEnvironment>
      <App />
    </ChakraEnvironment>
  </React.StrictMode>
);

