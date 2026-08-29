import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { fetchWithRetry } from "./fetch-with-retry";

const okResponse = (body: unknown = {}): Response =>
  new Response(JSON.stringify(body), { status: 200, headers: { "content-type": "application/json" } });

const transientResponse = (status: number): Response =>
  new Response("upstream not ready", { status });

describe("fetchWithRetry", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => { vi.useRealTimers(); });

  it("returns the response on first success", async () => {
    const fetchSpy = vi.fn().mockResolvedValueOnce(okResponse({ ok: true }));
    vi.stubGlobal("fetch", fetchSpy);

    const res = await fetchWithRetry("/api/configuration");

    expect(res.ok).toBe(true);
    expect(fetchSpy).toHaveBeenCalledTimes(1);
  });

  it("retries 502s until a 200 lands", async () => {
    const fetchSpy = vi.fn()
      .mockResolvedValueOnce(transientResponse(502))
      .mockResolvedValueOnce(transientResponse(503))
      .mockResolvedValueOnce(okResponse({ ready: true }));
    vi.stubGlobal("fetch", fetchSpy);

    const promise = fetchWithRetry("/api/configuration");
    // Drain the backoff timers so the loop re-enters before we assert.
    await vi.runAllTimersAsync();
    const res = await promise;

    expect(res.ok).toBe(true);
    expect(fetchSpy).toHaveBeenCalledTimes(3);
  });

  it("retries network errors", async () => {
    const fetchSpy = vi.fn()
      .mockRejectedValueOnce(new TypeError("Failed to fetch"))
      .mockResolvedValueOnce(okResponse());
    vi.stubGlobal("fetch", fetchSpy);

    const promise = fetchWithRetry("/api/monitors");
    await vi.runAllTimersAsync();
    const res = await promise;

    expect(res.ok).toBe(true);
    expect(fetchSpy).toHaveBeenCalledTimes(2);
  });

  it("throws on 4xx responses without retrying", async () => {
    const fetchSpy = vi.fn().mockResolvedValueOnce(transientResponse(404));
    vi.stubGlobal("fetch", fetchSpy);

    await expect(fetchWithRetry("/api/missing")).rejects.toThrow(/HTTP 404/);
    expect(fetchSpy).toHaveBeenCalledTimes(1);
  });
});
