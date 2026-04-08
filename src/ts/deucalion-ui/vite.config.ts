/// <reference types="vitest/config" />

import react from "@vitejs/plugin-react";
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from "vite";
import { visualizer } from "rollup-plugin-visualizer";
import viteCompression from "vite-plugin-compression";

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
      viteCompression({
        algorithm: 'brotliCompress',
        ext: '.br',
        threshold: 256,
      }),
      viteCompression({
        algorithm: 'gzip',
        ext: '.gz',
        threshold: 256,
        compressionOptions: { level: 9 },
      }),
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
