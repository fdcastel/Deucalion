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
  // One retry hedges against a runner hiccup. More than that mostly hides
  // real regressions, and every assertion already carries a generous timeout.
  retries: process.env.CI ? 1 : 0,
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
      // -c Release --no-build reuses the build CI already produced. Without it
      // `dotnet run` defaults to Debug and recompiles the whole solution
      // mid-run. Locally, `Invoke-Build` produces the Release build first.
      command:
        "dotnet run --project ../../cs/Deucalion.Api -c Release --no-build --no-launch-profile --urls http://localhost:5000",
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
