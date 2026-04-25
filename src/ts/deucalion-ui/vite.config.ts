/// <reference types="vitest/config" />

import solid from "vite-plugin-solid";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";
import { visualizer } from "rollup-plugin-visualizer";
import viteCompression from "vite-plugin-compression";

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const isAnalyze = mode === "analyze";
  const isTest = mode === "test" || process.env.VITEST === "true";

  return {
    build: {
      chunkSizeWarningLimit: 200,
    },
    plugins: [
      tailwindcss(),
      // Disable solid's HMR plugin under vitest — solid-refresh tries to
      // resolve the JSX files via "file:///@solid-refresh" which jsdom
      // can't open.
      solid({ hot: !isTest }),
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
      globals: false,
      // Make sure every Solid import resolves to the same runtime — the
      // "multiple instances of Solid" warning leads to broken reactivity
      // between test code and components.
      server: {
        deps: {
          inline: [/solid-js/, /@solidjs\/testing-library/],
        },
      },
      exclude: ["node_modules", "dist", "tests/e2e/**"],
    },
    resolve: {
      conditions: isTest ? ["development", "browser"] : undefined,
      dedupe: ["solid-js", "solid-js/web", "solid-js/store"],
    },
  };
});
