import { render, waitFor } from "@solidjs/testing-library";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { __resetMonitorsForTests, refetchMonitors } from "./stores/monitors-store";
import { App } from "./app";

// index.html ships the splash; the app only hides it. Recreate it per test.
const mountSplash = (): HTMLElement => {
  const splash = document.createElement("div");
  splash.id = "splash";
  document.body.appendChild(splash);
  return splash;
};

const jsonResponse = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

describe("<App> splash lifecycle", () => {
  let splash: HTMLElement;

  beforeEach(() => {
    __resetMonitorsForTests();
    splash = mountSplash();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    splash.remove();
  });

  it("hides the splash once both resources resolve", async () => {
    // The setup-file fetch stub answers both /api/configuration and /api/monitors.
    refetchMonitors();
    render(() => <App />);

    await waitFor(() => { expect(splash).toHaveClass("hidden"); });
    expect(document.querySelector(".brand-name")?.textContent).not.toBe("Something went wrong");
  });

  it("surfaces a fatal /api/monitors fetch instead of hanging on the splash (#17)", async () => {
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input: RequestInfo | URL) => {
      const url = typeof input === "string" ? input : input.toString();
      if (/\/api\/monitors(\?|$)/.test(url)) return new Response("nope", { status: 404, statusText: "Not Found" });
      return jsonResponse({ pageTitle: "Test" });
    });

    // Re-run the initial fetch against the 404 stub. 4xx is fatal (no retry), so
    // the resource rejects; before the fix nothing ever read it and the rejection
    // was swallowed.
    refetchMonitors();
    render(() => <App />);

    await waitFor(() => {
      expect(document.querySelector(".brand-name")?.textContent).toBe("Something went wrong");
    });
    expect(document.querySelector("pre.mono")?.textContent).toContain("HTTP 404");
    expect(splash).toHaveClass("hidden");
  });
});
