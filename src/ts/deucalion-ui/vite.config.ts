import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// https://vitejs.dev/config/
export default defineConfig({
  build: {
    chunkSizeWarningLimit: 1000,
  },
  plugins: [
    react(),
    {
      // Removes pure annotations from SignalR -- https://github.com/dotnet/aspnetcore/issues/55286#issuecomment-2557288741
      name: 'remove-pure-annotations',
      enforce: 'pre',
      transform: (code, id) =>
        id.indexOf('node_modules/@microsoft/signalr') !== -1
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
