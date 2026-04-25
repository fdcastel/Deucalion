import { expect, test } from "@playwright/test";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs/promises";

// iPhone 13 viewport + user-agent. We can't borrow `devices["iPhone 13"]`
// because it sets `defaultBrowserType: "webkit"`, which can't be applied
// inside describe(). The viewport is what actually triggers the
// max-width: 720px CSS rules, so this is enough for the QA pass.
const IPHONE_13 = {
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 3,
  isMobile: true,
  hasTouch: true,
  userAgent:
    "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 " +
    "(KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1",
};

// Mobile visual-QA pass: drives the dashboard at iPhone-sized viewports and
// captures full-page screenshots so the reviewer can confirm the mobile-only
// trims (no event feed, no trend sparkline, no "disconnected" label, no
// tweaks-panel button) actually land on small screens.

const QA_DIR = path.resolve(fileURLToPath(import.meta.url), "../../../../../../tmp/visual-qa/mobile");

const ensureDir = async (): Promise<string> => {
  await fs.mkdir(QA_DIR, { recursive: true });
  return QA_DIR;
};

test.use(IPHONE_13);

test.describe("mobile visual QA", () => {
  test("dashboard hides feed, sparkline, connection label and tweaks button", async ({ page }) => {
    const outDir = await ensureDir();
    await page.goto("/");
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });
    await page.waitForTimeout(400);

    await page.screenshot({ path: path.join(outDir, "iphone13-dark-full.png"), fullPage: true });

    await page.locator(".topbar").screenshot({ path: path.join(outDir, "iphone13-dark-topbar.png") });
    await page.locator(".hero").screenshot({ path: path.join(outDir, "iphone13-dark-hero.png") });

    // The live-event feed is gone entirely (was already hidden on mobile,
    // now removed from desktop too — see hero.tsx).
    await expect(page.locator(".feed")).toHaveCount(0);
    await expect(page.locator(".hero-spark-wrap")).toBeHidden();
    await expect(page.locator(".connection-label")).toBeHidden();
    await expect(page.getByRole("button", { name: "Open tweaks panel" })).toHaveCount(0);
    // Type badges are hidden on mobile to give the monitor name more room.
    await expect(page.locator(".type-badge").first()).toBeHidden();

    // Incident text shouldn't overflow into the heartbeat strip. For each
    // row, the incident text's right edge must stay within the row, and
    // its left edge must sit to the right of the heartbeat strip.
    const rows = await page.locator(".row").all();
    for (const row of rows) {
      const rowBox = await row.boundingBox();
      const incidentBox = await row.locator(".last-incident").boundingBox();
      const stripBox = await row.locator(".col-strip").boundingBox();
      expect(rowBox).not.toBeNull();
      expect(incidentBox).not.toBeNull();
      expect(stripBox).not.toBeNull();
      if (rowBox && incidentBox && stripBox) {
        expect(incidentBox.x + incidentBox.width).toBeLessThanOrEqual(rowBox.x + rowBox.width + 1);
        expect(incidentBox.x).toBeGreaterThanOrEqual(stripBox.x + stripBox.width - 1);
      }
    }
    // Capture a row screenshot (one with incident text) so the reviewer
    // can sanity-check there's no visible overlap.
    await page.locator(".row.is-down").first().screenshot({
      path: path.join(outDir, "iphone13-dark-row.png"),
    });

    // No separator line between rows — the bottom border was removed.
    const firstRow = page.locator(".row").first();
    const borderBottom = await firstRow.evaluate((el) =>
      window.getComputedStyle(el).borderBottomWidth,
    );
    expect(borderBottom).toBe("0px");

    // Counters fit on one line: hero-summary-row should not wrap.
    const summary = page.locator(".hero-summary-row");
    await expect(summary).toBeVisible();
    const box = await summary.boundingBox();
    const chips = await summary.locator(".hero-chip").all();
    let topY: number | null = null;
    for (const chip of chips) {
      const cb = await chip.boundingBox();
      if (!cb) continue;
      if (topY === null) topY = cb.y;
      else expect(Math.abs(cb.y - topY)).toBeLessThan(4);
    }
    expect(box).not.toBeNull();
  });

  test("light theme + secret window.deucalion() opens the tweaks panel", async ({ page }) => {
    const outDir = await ensureDir();
    await page.goto("/");
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });

    await page.evaluate(() => {
      const raw = localStorage.getItem("deucalion.tweaks");
      const parsed = raw ? JSON.parse(raw) as Record<string, unknown> : {};
      parsed.theme = "light";
      localStorage.setItem("deucalion.tweaks", JSON.stringify(parsed));
    });
    await page.reload();
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });
    await expect(page.locator("html")).toHaveAttribute("data-theme", "light");

    await page.waitForTimeout(400);
    await page.screenshot({ path: path.join(outDir, "iphone13-light-full.png"), fullPage: true });

    // Secret easter-egg: the panel can only be opened from JS now.
    await expect(page.locator(".twk-panel")).toHaveCount(0);
    await page.evaluate(() => {
      (window as unknown as { deucalion: () => void }).deucalion();
    });
    await expect(page.locator(".twk-panel")).toBeVisible();
    await page.waitForTimeout(200);
    await page.screenshot({ path: path.join(outDir, "iphone13-light-tweaks-open.png"), fullPage: false });
  });
});
