import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

import importMetaEnv from "@import-meta-env/unplugin";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react(), importMetaEnv.vite({ example: ".env" })],
  server: {
    proxy: {
      "/api/monitors/hub": {
        target: "ws://localhost:5000",
        ws: true,
      },
      "/api/monitors": {
        target: "http://localhost:5000",
      },
    },
    port: 5173,
  },
});
