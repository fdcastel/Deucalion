import { expect, test, type Page } from "@playwright/test";

// Waits for the SPA to swap the splash out and render the monitor list.
const waitForDashboard = async (page: Page): Promise<void> => {
  await expect(page.locator(".row").first()).toBeVisible({ timeout: 30_000 });
};

test.describe("Deucalion dashboard", () => {
  test("renders the brand icon, top bar and a row per monitor the API returns", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    await expect(page.locator("img.brand-icon")).toBeVisible();
    await expect(page.locator(".brand-name")).toContainText(/Deucalion|status/i);

    // Derived from the API rather than hard-coded, so adding a monitor to
    // deucalion-sample.yaml does not break CI. This also asserts something
    // stronger: the UI renders every monitor the backend reports.
    const response = await page.request.get("/api/monitors");
    const expected = (await response.json() as unknown[]).length;
    expect(expected).toBeGreaterThan(0);
    await expect(page.locator(".row")).toHaveCount(expected);
  });

  test("connection dot turns green once SSE is open", async ({ page }) => {
    await page.goto("/");
    const dot = page.locator(".connection-dot");
    await expect(dot).toBeVisible();
    await expect(dot).not.toHaveClass(/connecting|error/, { timeout: 30_000 });
  });

  test("type badge uses the backend-provided type", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    // The sample yaml mixes ping, http, dns and checkin.
    const badgeClasses = await page.locator(".type-badge").evaluateAll((els) =>
      els.map((el) => Array.from(el.classList).find((c) => c.startsWith("t-")) ?? null),
    );
    expect(badgeClasses).not.toContain(null);
    expect(new Set(badgeClasses).size).toBeGreaterThan(1);
  });

  test("event feed is no longer rendered (data still flows via SSE)", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);
    await expect(page.locator(".feed")).toHaveCount(0);
  });
});

test.describe("Wire contract", () => {
  // Nothing else catches a rename of the short SSE keys (n/at/fr/st/ms/ns),
  // which are mirrored by hand between Deucalion.Api/Models/*.cs and
  // src/services/deucalion-types.ts. The heartbeat ticks rendered on load come
  // from GET /api/monitors, so only a *live* update exercises mergeChecked.
  test("live SSE updates flow into the heartbeat strip", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    // The newest tick sits at the right-hand end and its tooltip is built from
    // the wire fields (st, at, ms). A rename on the C# side stops it changing.
    const newestTick = page.locator(".row").first().locator(".tick").last();
    const before = await newestTick.getAttribute("data-tip");
    expect(before).not.toBeNull();

    await expect(newestTick).not.toHaveAttribute("data-tip", before ?? "", { timeout: 30_000 });
  });

  test("stats served by the API populate the availability column", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    // Asserts the camelCase stats keys (availability) still line up.
    await expect(page.locator(".avail").first()).toHaveText(/^\d+\.\d{2}%$/);
  });

  test("every wire field reaches the UI: state, timestamp and latency", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    // Each tick tooltip is "<state> · <time>[ · <latency>]", built from st/at/ms.
    // Renaming any of those on the C# side drops the corresponding segment, so
    // asserting the full shape catches a rename that a "did it change" check
    // would sail past.
    const tips = await page.locator(".tick[data-tip]").evaluateAll((els) =>
      els.map((el) => el.getAttribute("data-tip") ?? ""),
    );
    expect(tips.length).toBeGreaterThan(0);

    // st -> a state label, at -> a HH:MM:SS time.
    expect(tips.every((t) => /^(Up|Down|Warn|Degraded|Unknown)\b/.test(t))).toBe(true);
    expect(tips.every((t) => /\d{1,2}:\d{2}/.test(t))).toBe(true);

    // ms -> at least one probe reports a latency (the sample yaml has plenty).
    expect(tips.some((t) => /·\s*\d+(\.\d+)?(ms|s)$/.test(t))).toBe(true);
  });
});

test.describe("Heartbeat strip", () => {
  // The strip length is viewport-tiered: 60 (mobile) / 90 / 120 (wide desktop).
  const tiers = [
    { width: 390, height: 844, expected: 60 },
    { width: 1280, height: 720, expected: 90 },
    { width: 1480, height: 900, expected: 120 },
  ];

  for (const { width, height, expected } of tiers) {
    test(`renders ${expected.toString()} ticks at ${width.toString()}px`, async ({ page }) => {
      await page.setViewportSize({ width, height });
      await page.goto("/");
      await waitForDashboard(page);

      await expect(page.locator(".row").first().locator(".tick")).toHaveCount(expected);
    });
  }
});

test.describe("Layout", () => {
  test("rows have no bottom border and the latency popover is hidden until hover", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    const borderWidth = await page.locator(".row").first()
      .evaluate((el) => getComputedStyle(el).borderBottomWidth);
    expect(borderWidth).toBe("0px");

    const popover = page.locator(".row").first().locator(".lat-stats-pop");
    const atRest = await popover.evaluate((el) => Number(getComputedStyle(el).opacity));
    expect(atRest).toBeLessThan(0.05);

    await page.locator(".row").first().locator(".col-stats").hover();
    await expect.poll(async () =>
      popover.evaluate((el) => Number(getComputedStyle(el).opacity)),
    ).toBeGreaterThan(0.9);
  });

  test("mobile viewport trims the hero meta, trend sparkline and connection label", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/");
    await waitForDashboard(page);

    // There are two .hero-meta elements (left and right captions).
    await expect(page.locator(".hero-meta").first()).toBeHidden();
    await expect(page.locator(".hero-meta").last()).toBeHidden();
    await expect(page.locator(".hero-spark-wrap")).toBeHidden();
    await expect(page.locator(".connection-label")).toBeHidden();
  });
});

test.describe("Base styling", () => {
  // These replace the framework preflight that came with Tailwind. Without
  // them the icons sit on the text baseline and links go default-blue, so they
  // are asserted rather than eyeballed.
  test("splash fades out instead of disappearing instantly", async ({ page }) => {
    await page.goto("/");

    // Catch the splash before app.tsx removes it (400ms after adding .hidden).
    const splashStyles = await page.evaluate(() => {
      const el = document.getElementById("splash");
      if (!el) return null;
      const cs = getComputedStyle(el);
      return { display: cs.display, transition: cs.transitionProperty };
    });

    if (splashStyles !== null) {
      // A leftover `.hidden { display: none }` utility would win here.
      expect(splashStyles.display).not.toBe("none");
      expect(splashStyles.transition).toContain("opacity");
    }

    await waitForDashboard(page);
    await expect(page.locator("#splash")).toHaveCount(0);
  });

  test("buttons inherit the page font and centre their icons", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    const bodyFont = await page.evaluate(() => getComputedStyle(document.body).fontFamily);
    const themeBtnFont = await page.getByRole("button", { name: "Toggle theme" })
      .evaluate((el) => getComputedStyle(el).fontFamily);
    expect(themeBtnFont).toBe(bodyFont);

    await page.evaluate(() => {
      (window as unknown as { deucalion: () => void }).deucalion();
    });
    await expect(page.locator(".twk-panel")).toBeVisible();

    const closeStyles = await page.getByRole("button", { name: "Close tweaks" })
      .evaluate((el) => {
        const cs = getComputedStyle(el);
        return { display: cs.display, alignItems: cs.alignItems };
      });
    // The rule says inline-flex; a flex parent blockifies it to flex. Either
    // means the icon is centred rather than sitting on the text baseline.
    expect(["inline-flex", "flex"]).toContain(closeStyles.display);
    expect(closeStyles.alignItems).toBe("center");
  });

  test("SVG icons are blockified so they do not sit on the text baseline", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    const display = await page.locator("svg").first()
      .evaluate((el) => getComputedStyle(el).display);
    // Flex/grid children get blockified anyway; both are acceptable.
    expect(["block", "flex"]).toContain(display);
  });

  test("links are not default-blue or underlined", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    const link = page.locator(".row-name a").first();
    if (await link.count() > 0) {
      const styles = await link.evaluate((el) => {
        const cs = getComputedStyle(el);
        return { color: cs.color, decoration: cs.textDecorationLine };
      });
      expect(styles.decoration).toBe("none");
      // The UA default is rgb(0, 0, 238); ours inherits the row colour.
      expect(styles.color).not.toBe("rgb(0, 0, 238)");
    }
  });
});

test.describe("Tweaks panel", () => {
  test("opens via the window.deucalion() easter-egg and closes via the X button", async ({ page }) => {
    await page.goto("/");
    await waitForDashboard(page);

    // The visible trigger is gone; the panel is summoned from the console.
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
    await waitForDashboard(page);

    // The FOUC script picks up the OS preference, which varies by runner.
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
});
