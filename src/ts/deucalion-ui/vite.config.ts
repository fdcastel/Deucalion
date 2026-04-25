/// <reference types="vitest/config" />

import solid from "vite-plugin-solid";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";
import { visualizer } from "rollup-plugin-visualizer";
import viteCompression from "vite-plugin-compression";

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const isAnalyze = mode === "analyze";

  return {
    build: {
      chunkSizeWarningLimit: 200,
    },
    plugins: [
      tailwindcss(),
      solid(),
      viteCompression({
        algorithm: "brotliCompress",
        ext: ".br",
        threshold: 256,
      }),
      viteCompression({
        algorithm: "gzip",
        ext: ".gz",
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
