import { expect, test } from "@playwright/test";

test.describe("Deucalion dashboard", () => {
  test("renders the brand icon, top bar and a row per configured monitor", async ({ page }) => {
    await page.goto("/");

    // Wait for the SPA to swap the splash out and render rows.
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });

    // Brand icon (the satellite-dish SVG, not the prototype's CSS dot)
    await expect(page.locator("img.brand-icon")).toBeVisible();
    await expect(page.locator(".brand-name")).toContainText(/Deucalion|status/i);

    // The sample yaml has 10 monitors.
    const rows = page.locator(".row");
    await expect(rows).toHaveCount(10);
  });

  test("connection dot turns green once SSE is open", async ({ page }) => {
    await page.goto("/");
    const dot = page.locator(".connection-dot");
    await expect(dot).toBeVisible();
    // Initially might be `.connecting`; wait for the steady state.
    await expect(dot).not.toHaveClass(/connecting|error/, { timeout: 30_000 });
  });

  test("at least one row picks up the heartbeat strip", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });
    // Strip length is viewport-tier'd (60 / 90 / 120). The Playwright
    // default desktop viewport (1280×720) lands in the 90 tier.
    const ticks = page.locator(".row").first().locator(".tick");
    const expected = await page.evaluate(() => {
      const w = window.innerWidth;
      if (w >= 1480) return 120;
      if (w >= 1280) return 90;
      return 60;
    });
    await expect(ticks).toHaveCount(expected);
  });

  test("type badge uses the backend-provided type", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });

    // Sample yaml mixes ping, http, dns, checkin — every row should have a
    // recognisable type-* class.
    const badgeClasses = await page.locator(".type-badge").evaluateAll((els) =>
      els.map((el) => Array.from(el.classList).find((c) => c.startsWith("t-")) ?? null),
    );
    expect(badgeClasses).not.toContain(null);
    expect(new Set(badgeClasses).size).toBeGreaterThan(1);
  });
});

test.describe("Tweaks panel", () => {
  test("opens via the window.deucalion() easter-egg and closes via the X button", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });

    // The visible trigger is gone; the panel is now summoned from the console.
    await expect(page.getByRole("button", { name: "Open tweaks panel" })).toHaveCount(0);
    await expect(page.locator(".twk-panel")).toHaveCount(0);

    await page.evaluate(() => {
      (window as unknown as { deucalion: () => void }).deucalion();
    });
    await expect(page.locator(".twk-panel")).toBeVisible();

    await page.getByRole("button", { name: "Close tweaks" }).click();
    await expect(page.locator(".twk-panel")).toHaveCount(0);
  });

  test("theme selection persists across reload", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });

    // Note the starting theme — the FOUC script picks up the OS preference,
    // which can be either "dark" or "light" depending on the runner.
    const initial = await page.locator("html").getAttribute("data-theme");
    const target = initial === "dark" ? "light" : "dark";

    await page.getByRole("button", { name: "Toggle theme" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", target);

    // The Solid createEffect writes localStorage in the same tick; verify it
    // landed before reloading so we're not racing the FOUC script.
    const stored = await page.evaluate(() => localStorage.getItem("deucalion.tweaks"));
    expect(stored && JSON.parse(stored).theme).toBe(target);

    await page.reload();
    await expect(page.locator("html")).toHaveAttribute("data-theme", target);
  });
});

test.describe("Hero", () => {
  test("renders aggregate availability and per-state chips", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator(".hero-availability")).toBeVisible({ timeout: 30_000 });
    await expect(page.locator(".hero-availability")).toContainText(/%/);
    await expect(page.locator(".hero-chip.up")).toBeVisible();
  });

  test("event feed is no longer rendered (data still flows via SSE)", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator(".hero-availability")).toBeVisible({ timeout: 30_000 });
    await expect(page.locator(".feed")).toHaveCount(0);
  });
});
