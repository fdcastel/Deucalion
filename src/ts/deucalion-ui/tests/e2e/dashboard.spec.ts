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
    // Each row renders 60 ticks (some may be unknown padding)
    const ticks = page.locator(".row").first().locator(".tick");
    await expect(ticks).toHaveCount(60);
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
  test("opens via the discreet trigger and closes via the X button", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });

    const trigger = page.getByRole("button", { name: "Open tweaks panel" });
    await expect(page.locator(".twk-panel")).toHaveCount(0);
    await trigger.click();
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

  test("event feed appears alongside the hero stat", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator(".feed")).toBeVisible({ timeout: 30_000 });
    // The header uses two `.feed-title` blocks — the first is the "Live events" label.
    await expect(page.locator(".feed-title").first()).toContainText(/Live events/i);
  });
});
