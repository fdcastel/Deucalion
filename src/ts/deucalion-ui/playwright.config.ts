import { defineConfig, devices } from "@playwright/test";

const PORT = 5173;
const BASE_URL = `http://localhost:${PORT.toString()}`;

// The dev server proxies /api → http://localhost:5000 (the .NET backend).
// Boot both as webServers so `npm run test:e2e` is one-command:
//
//   1. Backend (Deucalion.Api) on :5000, pointed at the sample yaml.
//   2. Vite dev server on :5173.
//
// If either is already running locally, Playwright will reuse the existing
// process (`reuseExistingServer: !CI`).
export default defineConfig({
  testDir: "./tests/e2e",
  fullyParallel: false,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: [["list"]],

  use: {
    baseURL: BASE_URL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },

  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  ],

  webServer: [
    {
      // The project's appsettings.Development.json already points at the
      // repo's deucalion-sample.yaml; we just override the storage path
      // so e2e runs don't share state with manual dev sessions.
      command:
        "dotnet run --project ../../cs/Deucalion.Api --no-launch-profile --urls http://localhost:5000",
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        DEUCALION__STORAGEPATH: "./.e2e-storage",
      },
      url: "http://localhost:5000/api/configuration",
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      stdout: "pipe",
      stderr: "pipe",
    },
    {
      command: "npm run dev",
      url: BASE_URL,
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
      stdout: "pipe",
      stderr: "pipe",
    },
  ],
});
