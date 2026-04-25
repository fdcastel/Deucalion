import { expect, test } from "@playwright/test";
import { spawn, type ChildProcess } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs/promises";

// One-off visual QA harness that drives both the V5 dashboard and the
// design prototype, captures full-page + per-region screenshots in
// dark and light themes, and writes them to tmp/visual-qa so the
// reviewer can inspect them side-by-side.
//
// Run with:
//   npx playwright test tests/e2e/visual-qa.spec.ts --reporter=list

const QA_DIR = path.resolve(fileURLToPath(import.meta.url), "../../../../../../tmp/visual-qa");
const PROTOTYPE_DIR = path.resolve(fileURLToPath(import.meta.url), "../../../../../../tmp/New-Deucalion");
const PROTOTYPE_PORT = 5180;
const PROTOTYPE_HTML = `http://localhost:${PROTOTYPE_PORT.toString()}/Deucalion%20Redesign.html`;

let httpServer: ChildProcess | undefined;

const waitForServer = async (url: string, timeoutMs = 30_000): Promise<void> => {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url);
      if (res.ok) return;
    } catch {
      /* server not up yet */
    }
    await new Promise<void>((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`Timed out waiting for ${url}`);
};

const ensureDir = async (sub: string): Promise<string> => {
  const target = path.join(QA_DIR, sub);
  await fs.mkdir(target, { recursive: true });
  return target;
};

const setTheme = async (
  page: import("@playwright/test").Page,
  scope: "v5" | "prototype",
  theme: "dark" | "light",
): Promise<void> => {
  if (scope === "v5") {
    await page.evaluate((t) => {
      try {
        const raw = localStorage.getItem("deucalion.tweaks");
        const parsed = raw ? JSON.parse(raw) as Record<string, unknown> : {};
        parsed.theme = t;
        localStorage.setItem("deucalion.tweaks", JSON.stringify(parsed));
      } catch { /* ignore */ }
    }, theme);
    await page.reload();
    // Wait for at least one row before screenshotting.
    await page.locator(".row").first().waitFor({ state: "visible", timeout: 30_000 });
    await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
  } else {
    // Prototype defaults to dark; flip via the inline theme button when needed.
    const current = await page.locator("html").getAttribute("data-theme");
    if (current !== theme) {
      await page.locator(".theme-btn").click();
    }
    await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
  }
};

const captureRegions = async (
  page: import("@playwright/test").Page,
  outDir: string,
  scope: "v5" | "prototype",
  theme: "dark" | "light",
): Promise<void> => {
  await page.waitForTimeout(400); // let fonts settle
  const prefix = `${scope}-${theme}`;

  await page.screenshot({ path: path.join(outDir, `${prefix}-full.png`), fullPage: true });

  const topbar = page.locator(".topbar");
  if (await topbar.count() > 0) {
    await topbar.screenshot({ path: path.join(outDir, `${prefix}-topbar.png`) });
  }

  const hero = page.locator(".hero");
  if (await hero.count() > 0) {
    await hero.screenshot({ path: path.join(outDir, `${prefix}-hero.png`) });
  }

  // First monitor row.
  const firstRow = page.locator(".row").first();
  if (await firstRow.count() > 0) {
    await firstRow.screenshot({ path: path.join(outDir, `${prefix}-row.png`) });
  }

  // Group/section header (V5 has just one).
  const groupHeader = page.locator(".group-header").first();
  if (await groupHeader.count() > 0) {
    await groupHeader.screenshot({ path: path.join(outDir, `${prefix}-group-header.png`) });
  }

  // Footer (V5 + prototype both have one).
  const footer = page.locator(".footer");
  if (await footer.count() > 0) {
    await footer.screenshot({ path: path.join(outDir, `${prefix}-footer.png`) });
  }
};

test.describe.configure({ mode: "serial" });

test.describe("visual QA — V5 dashboard vs. prototype", () => {
  test.use({ viewport: { width: 1480, height: 1100 } });

  // Boot a one-off http-server that serves the prototype HTML. Chromium
  // refuses to fetch the .jsx files Babel needs over file://, so we need
  // a real HTTP origin. Lazily-started here so the dashboard.spec.ts run
  // (which doesn't need the prototype) doesn't pay the boot cost.
  test.beforeAll(async () => {
    httpServer = spawn(
      "npx",
      ["http-server", PROTOTYPE_DIR, "-p", PROTOTYPE_PORT.toString(), "-s", "--cors"],
      { stdio: "ignore", shell: true },
    );
    await waitForServer(PROTOTYPE_HTML, 60_000);
  });

  test.afterAll(() => {
    httpServer?.kill();
    httpServer = undefined;
  });

  test("V5 — dark + light", async ({ page }) => {
    const outDir = await ensureDir("v5");
    await page.goto("/");
    await page.locator(".row").first().waitFor({ state: "visible", timeout: 30_000 });
    await setTheme(page, "v5", "dark");
    await captureRegions(page, outDir, "v5", "dark");

    await setTheme(page, "v5", "light");
    await captureRegions(page, outDir, "v5", "light");

    // Tweaks panel (open) — dark only.
    await setTheme(page, "v5", "dark");
    await page.getByRole("button", { name: "Open tweaks panel" }).click();
    await page.locator(".twk-panel").waitFor({ state: "visible" });
    await page.waitForTimeout(200);
    await page.screenshot({ path: path.join(outDir, "v5-dark-tweaks-open.png"), fullPage: false });
  });

  test("Prototype — dark + light", async ({ page }) => {
    test.setTimeout(180_000);
    const outDir = await ensureDir("prototype");
    await page.goto(PROTOTYPE_HTML);
    // Babel-transformed app needs a moment to compile + render.
    await page.locator(".row").first().waitFor({ state: "visible", timeout: 90_000 });
    await captureRegions(page, outDir, "prototype", "dark");

    await setTheme(page, "prototype", "light");
    await captureRegions(page, outDir, "prototype", "light");
  });
});
