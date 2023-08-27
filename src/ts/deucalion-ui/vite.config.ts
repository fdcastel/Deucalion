import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
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
