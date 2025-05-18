import { defineConfig } from "vite";

import react from "@vitejs/plugin-react-swc"

import tsconfigPaths from "vite-tsconfig-paths"

// https://vite.dev/config/
export default defineConfig({
  build: {
    chunkSizeWarningLimit: 1000,
  },
  plugins: [
    react(), 
    tsconfigPaths(),
    {
      // Removes pure annotations from SignalR -- https://github.com/dotnet/aspnetcore/issues/55286#issuecomment-2557288741
      name: 'remove-pure-annotations',
      enforce: 'pre',
      transform: (code, id) => 
        id.includes('node_modules/@microsoft/signalr')
          ? code.replace(/\/\*#__PURE__\*\//g, '')
          : null
    }
  ],
  server: {
    proxy: {
      "/api/monitors/hub": {
        target: "ws://localhost:5000",
        ws: true,
      },
      "/api": {
        target: "http://localhost:5000",
      },
    },
    port: 5173,
  },
});
