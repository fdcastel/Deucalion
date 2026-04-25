import { expect, test } from "@playwright/test";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs/promises";

// Desktop QA: confirms rows are now compact (no border-bottom, no
// permanent latency stats below the sparkline) and that the percentile
// readout shows up on hover/click as a popover. Captures screenshots
// to tmp/visual-qa/desktop/.

const QA_DIR = path.resolve(fileURLToPath(import.meta.url), "../../../../../../tmp/visual-qa/desktop");

const ensureDir = async (): Promise<string> => {
  await fs.mkdir(QA_DIR, { recursive: true });
  return QA_DIR;
};

test.use({ viewport: { width: 1480, height: 1100 } });

test.describe("desktop visual QA", () => {
  test("rows are compact, no separator line, lat-stats hidden by default", async ({ page }) => {
    const outDir = await ensureDir();
    await page.goto("/");
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });
    await page.waitForTimeout(400);

    await page.screenshot({ path: path.join(outDir, "desktop-dark-full.png"), fullPage: true });

    // No separator line between rows.
    const firstRow = page.locator(".row").first();
    const border = await firstRow.evaluate((el) =>
      window.getComputedStyle(el).borderBottomWidth,
    );
    expect(border).toBe("0px");

    // Latency stats popover renders but is hidden until interaction.
    const pop = firstRow.locator(".lat-stats-pop");
    await expect(pop).toHaveCount(1);
    const opacity = await pop.evaluate((el) =>
      window.getComputedStyle(el).opacity,
    );
    expect(Number(opacity)).toBeLessThan(0.05);
  });

  test("hover and click reveal the latency percentile popover", async ({ page }) => {
    const outDir = await ensureDir();
    await page.goto("/");
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });

    const colStats = page.locator(".row").first().locator(".col-stats");
    const pop = page.locator(".row").first().locator(".lat-stats-pop");

    // Hover -> popover becomes visible.
    await colStats.hover();
    await expect(pop).toBeVisible();
    // Transition is ~120ms; poll until it has finished settling.
    await expect.poll(async () =>
      Number(await pop.evaluate((el) => window.getComputedStyle(el).opacity)),
    ).toBeGreaterThan(0.9);

    await page.waitForTimeout(150);
    await page.screenshot({
      path: path.join(outDir, "desktop-dark-row-hover.png"),
      clip: {
        x: 0,
        y: (await page.locator(".row").first().boundingBox())?.y ?? 0,
        width: 1480,
        height: 200,
      },
    });

    // Click toggle -> still visible (and stays visible after pointer leaves).
    await page.mouse.move(0, 0);
    await colStats.click();
    await expect(colStats).toHaveClass(/is-open/);
    await expect(pop).toBeVisible();

    // Click again -> closes.
    await colStats.click();
    await expect(colStats).not.toHaveClass(/is-open/);
  });
});
