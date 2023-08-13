import React from 'react'
import ReactDOM from 'react-dom/client'
import { ChakraProvider, ColorModeScript, type ThemeConfig } from "@chakra-ui/react";

import { App } from "./App";
import theme from "./theme";

const container = document.getElementById("root");
if (!container) throw new Error("Failed to find the root element");

ReactDOM.createRoot(container).render(
  <React.StrictMode>
    <ColorModeScript initialColorMode={(theme.config as ThemeConfig).initialColorMode} />
    <ChakraProvider theme={theme}>
    <App />
    </ChakraProvider>
  </React.StrictMode>,
)
