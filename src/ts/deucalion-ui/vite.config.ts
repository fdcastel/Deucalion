/// <reference types="vitest/config" />

import react from "@vitejs/plugin-react";
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from "vite";
import { visualizer } from "rollup-plugin-visualizer";

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const isAnalyze = mode === "analyze";

  return {
    build: {
      chunkSizeWarningLimit: 200, // Enforce strict budget: warn on chunks > 200 KB
      rollupOptions: {
        output: {
          // Attempt early code-splitting of vendor + app logic
          manualChunks(id: string) {
            // Isolate HeroUI/Toast to separate chunks for lazy loading
            if (id.includes('node_modules/@heroui')) {
              return 'heroui-vendor';
            }
            if (id.includes('node_modules/@microsoft/signalr')) {
              return 'signalr-vendor';
            }
            if (id.includes('node_modules/react') && !id.includes('node_modules/@')) {
              return 'react-vendor';
            }
          }
        }
      }
    },
    plugins: [
      tailwindcss(),
      react(),
      {
        // Removes pure annotations from SignalR -- https://github.com/dotnet/aspnetcore/issues/55286#issuecomment-2557288741
        name: 'remove-pure-annotations',
        enforce: 'pre',
        transform: (code, id) =>
          id.indexOf('node_modules/@microsoft/signalr') !== -1
            ? code.replace(/\/\*#__PURE__\*\//g, '')
            : null
      },
      ...(isAnalyze
        ? [
            visualizer({
              filename: "dist/bundle-stats.html",
              template: "treemap",
              gzipSize: true,
              brotliSize: true,
            }),
            visualizer({
              filename: "dist/bundle-stats.json",
              template: "raw-data",
              gzipSize: true,
              brotliSize: true,
            }),
          ]
        : []),
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
    test: {
      environment: "jsdom",
      setupFiles: "./src/test/setup.ts",
      css: true,
    },
  };
});
