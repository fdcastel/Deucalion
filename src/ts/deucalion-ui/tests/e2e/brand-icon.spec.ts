import { expect, test } from "@playwright/test";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs/promises";

// Capture the brand icon in both themes so the embossed/shadowed
// treatment can be eyeballed against the v4 reference.

const QA_DIR = path.resolve(fileURLToPath(import.meta.url), "../../../../../../tmp/visual-qa/brand-icon");

const ensureDir = async (): Promise<string> => {
  await fs.mkdir(QA_DIR, { recursive: true });
  return QA_DIR;
};

test.use({ viewport: { width: 1280, height: 720 } });

test.describe("brand icon — embossed look", () => {
  for (const theme of ["dark", "light"] as const) {
    test(`captures the brand icon in ${theme} theme`, async ({ page }) => {
      const outDir = await ensureDir();
      await page.goto("/");
      await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });

      await page.evaluate((t) => {
        const raw = localStorage.getItem("deucalion.tweaks");
        const parsed = raw ? JSON.parse(raw) as Record<string, unknown> : {};
        parsed.theme = t;
        localStorage.setItem("deucalion.tweaks", JSON.stringify(parsed));
      }, theme);
      await page.reload();
      await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
      await page.locator(".row").first().waitFor({ state: "visible", timeout: 30_000 });
      await page.waitForTimeout(300);

      // Tight crop around just the icon + a margin for the shadow halo.
      const icon = page.locator(".brand-icon");
      const box = await icon.boundingBox();
      expect(box).not.toBeNull();
      if (!box) return;
      const margin = 24;
      await page.screenshot({
        path: path.join(outDir, `brand-icon-${theme}.png`),
        clip: {
          x: Math.max(0, box.x - margin),
          y: Math.max(0, box.y - margin),
          width: box.width + margin * 2,
          height: box.height + margin * 2,
        },
      });
      // Wider crop showing the icon next to the brand title for visual context.
      const topbar = page.locator(".topbar");
      await topbar.screenshot({ path: path.join(outDir, `brand-icon-${theme}-topbar.png`) });
      // Full page so I can sanity-check the new background colour against
      // the rest of the dashboard (rows, hero, footer).
      await page.screenshot({
        path: path.join(outDir, `brand-icon-${theme}-page.png`),
        fullPage: true,
      });
      // Zoomed-in capture: temporarily blow the icon up to 6× so the
      // emboss/highlight pixels are clearly visible in the screenshot.
      await page.evaluate(() => {
        const el = document.querySelector(".brand-icon") as HTMLElement | null;
        if (!el) return;
        el.style.width = "192px";
        el.style.height = "192px";
      });
      await page.waitForTimeout(150);
      const bigBox = await icon.boundingBox();
      if (bigBox) {
        await page.screenshot({
          path: path.join(outDir, `brand-icon-${theme}-zoom.png`),
          clip: {
            x: Math.max(0, bigBox.x - margin),
            y: Math.max(0, bigBox.y - margin),
            width: bigBox.width + margin * 2,
            height: bigBox.height + margin * 2,
          },
        });
      }
    });
  }
});
