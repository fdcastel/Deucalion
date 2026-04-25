import "@testing-library/jest-dom/vitest";
import { afterEach, beforeEach, vi } from "vitest";
import { cleanup } from "@solidjs/testing-library";

// jsdom doesn't ship matchMedia; stub it to a stable implementation that
// returns "matches: false" so tweaks-store etc. fall through to defaults.
if (typeof window.matchMedia !== "function") {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

// Default fetch stub. Components that touch /api/* during rendering
// (configuration-store, monitors-store) get a stable, empty-ish response
// so they don't blow up jsdom's URL parser. Individual tests can override
// this with `vi.spyOn(globalThis, "fetch")`.
const defaultFetch = vi.fn(async (input: RequestInfo | URL): Promise<Response> => {
  const url = typeof input === "string" ? input : input.toString();
  if (url.endsWith("/api/configuration")) {
    return new Response(JSON.stringify({ pageTitle: "Test", pageDescription: "" }), {
      status: 200,
      headers: { "content-type": "application/json" },
    });
  }
  if (url.endsWith("/api/monitors")) {
    return new Response("[]", { status: 200, headers: { "content-type": "application/json" } });
  }
  return new Response("{}", { status: 200, headers: { "content-type": "application/json" } });
});
vi.stubGlobal("fetch", defaultFetch);

beforeEach(() => {
  // localStorage is shared across tests; ensure each one starts clean.
  localStorage.clear();
});

afterEach(() => {
  cleanup();
});
