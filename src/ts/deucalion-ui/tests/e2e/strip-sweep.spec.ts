import { expect, test } from "@playwright/test";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs/promises";

// Sweep the heartbeat strip at the viewports we care about and report
// each tier's measured tick width + how many ticks actually fit before
// the flexbox starts crushing them to the 2px minimum. Use the printed
// numbers to validate the 60 / 90 / 120 step function in
// heartbeat-strip.tsx, and the screenshots to eyeball the result.

const QA_DIR = path.resolve(fileURLToPath(import.meta.url), "../../../../../../tmp/visual-qa/strip-sweep");

const ensureDir = async (sub: string): Promise<string> => {
  const target = path.join(QA_DIR, sub);
  await fs.mkdir(target, { recursive: true });
  return target;
};

interface Sample {
  width: number;
  expectedLen: number;
  measuredLen: number;
  stripWidth: number;
  tickWidth: number;
  tickGap: number;
}

const VIEWPORTS = [
  { width: 1920, height: 1080, label: "1920" },
  { width: 1480, height: 1100, label: "1480" },
  { width: 1280, height: 900,  label: "1280" },
  { width: 1024, height: 768,  label: "1024" },
  { width: 768,  height: 900,  label: "768"  },
  { width: 390,  height: 844,  label: "390"  },
];

test.describe.configure({ mode: "serial" });

test.describe("heartbeat strip — viewport sweep", () => {
  for (const vp of VIEWPORTS) {
    test(`viewport ${vp.label}`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto("/");
      await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });
      await page.waitForTimeout(400);

      const outDir = await ensureDir(vp.label);
      await page.screenshot({ path: path.join(outDir, "full.png"), fullPage: true });

      const sample: Sample = await page.evaluate(() => {
        const w = window.innerWidth;
        const expectedLen = w >= 1480 ? 120 : w >= 1280 ? 90 : 60;
        const strip = document.querySelector(".row .col-strip") as HTMLElement | null;
        const ticks = strip?.querySelectorAll(".tick") ?? [];
        const sb = strip?.getBoundingClientRect();
        const t0 = ticks[0]?.getBoundingClientRect();
        const t1 = ticks[1]?.getBoundingClientRect();
        return {
          width: w,
          expectedLen,
          measuredLen: ticks.length,
          stripWidth: sb ? Math.round(sb.width) : 0,
          tickWidth: t0 ? Math.round(t0.width * 100) / 100 : 0,
          tickGap: t0 && t1 ? Math.round((t1.left - t0.right) * 100) / 100 : 0,
        };
      });

      console.log(`[strip-sweep ${vp.label}]`, JSON.stringify(sample));

      // Each tier should produce the expected tick count.
      expect(sample.measuredLen).toBe(sample.expectedLen);

      // Sanity: each tick must keep some visible width. If we ever drop
      // below 1.5px, ticks have collapsed to invisibility and the tier
      // boundary in stripLenForWidth needs a bump.
      expect(sample.tickWidth).toBeGreaterThanOrEqual(1.5);
    });
  }
});
