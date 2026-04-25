import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  __resetBackendPhaseForTests,
  backendPhase,
  fetchWithRetry,
} from "./fetch-with-retry";

const okResponse = (body: unknown = {}): Response =>
  new Response(JSON.stringify(body), { status: 200, headers: { "content-type": "application/json" } });

const transientResponse = (status: number): Response =>
  new Response("upstream not ready", { status });

describe("fetchWithRetry", () => {
  beforeEach(() => {
    __resetBackendPhaseForTests();
    vi.useFakeTimers();
  });
  afterEach(() => { vi.useRealTimers(); });

  it("returns the response and flips the phase to ready on first success", async () => {
    const fetchSpy = vi.fn().mockResolvedValueOnce(okResponse({ ok: true }));
    vi.stubGlobal("fetch", fetchSpy);

    const res = await fetchWithRetry("/api/configuration");

    expect(res.ok).toBe(true);
    expect(fetchSpy).toHaveBeenCalledTimes(1);
    expect(backendPhase()).toBe("ready");
  });

  it("retries 502s and surfaces 'waiting' until a 200 lands", async () => {
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
    expect(backendPhase()).toBe("ready");
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

  it("flips phase to 'waiting' while a transient retry loop is in flight", async () => {
    // Hold the second fetch indefinitely so the loop pauses with phase="waiting"
    // long enough for the assertion to land.
    let releaseSecond: (r: Response) => void = () => undefined;
    const fetchSpy = vi.fn()
      .mockResolvedValueOnce(transientResponse(502))
      .mockImplementationOnce(() => new Promise<Response>((resolve) => { releaseSecond = resolve; }));
    vi.stubGlobal("fetch", fetchSpy);

    const promise = fetchWithRetry("/api/configuration");
    // Drain the failed first attempt + the backoff sleep so the helper is now
    // sat on the second `await fetch(...)`.
    await vi.runAllTimersAsync();
    await Promise.resolve();
    expect(backendPhase()).toBe("waiting");

    releaseSecond(okResponse());
    await promise;
    expect(backendPhase()).toBe("ready");
  });
});
