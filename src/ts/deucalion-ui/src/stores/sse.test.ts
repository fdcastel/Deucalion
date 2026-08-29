import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { buildMonitor } from "../test/fixtures";
import { MonitorState } from "../services/deucalion-types";

import { __resetMonitorsForTests, __seedMonitorsForTests, monitors } from "./monitors-store";
import { __resetToastsForTests, toastList } from "./toast-store";
import { __resetSseForTests, connectSSE, sseStatus } from "./sse";

// A minimal stand-in for EventSource that records subscriptions so the
// test can fire payloads in the same shape the browser would.
class FakeEventSource {
  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSED = 2;
  static instances: FakeEventSource[] = [];

  url: string;
  readyState = FakeEventSource.CONNECTING;
  private listeners = new Map<string, Set<(e: MessageEvent<string> | Event) => void>>();

  constructor(url: string) {
    this.url = url;
    FakeEventSource.instances.push(this);
  }

  addEventListener(name: string, cb: (e: MessageEvent<string> | Event) => void): void {
    let set = this.listeners.get(name);
    if (!set) { set = new Set(); this.listeners.set(name, set); }
    set.add(cb);
  }

  removeEventListener(name: string, cb: (e: MessageEvent<string> | Event) => void): void {
    this.listeners.get(name)?.delete(cb);
  }

  close(): void {
    this.readyState = FakeEventSource.CLOSED;
    this.listeners.clear();
  }

  // Test helpers
  emit(name: string, payload?: unknown): void {
    const evt = payload === undefined
      ? new Event(name)
      : new MessageEvent(name, { data: typeof payload === "string" ? payload : JSON.stringify(payload) });
    this.listeners.get(name)?.forEach((cb) => { cb(evt); });
  }

  emitOpen(): void {
    this.readyState = FakeEventSource.OPEN;
    this.emit("open");
  }
}

const lastSource = (): FakeEventSource => {
  const last = FakeEventSource.instances.at(-1);
  if (!last) throw new Error("No EventSource instance was created");
  return last;
};

// Answers the resync fetch. Stubbed per test (rather than relying on the
// setup-file default) so a call count can be asserted, and never unstubbed
// wholesale: `vi.unstubAllGlobals()` would also drop the setup-file fetch stub
// and leave real (failing, endlessly retrying) fetches running in the background.
const okFetch = (): ReturnType<typeof vi.fn> =>
  vi.fn(async (input: RequestInfo | URL): Promise<Response> => {
    const url = typeof input === "string" ? input : input.toString();
    const body = /\/api\/monitors(\?|$)/.test(url) ? "[]" : "{}";
    return new Response(body, { status: 200, headers: { "content-type": "application/json" } });
  });

const monitorsFetches = (spy: ReturnType<typeof vi.fn>): number =>
  spy.mock.calls.filter(([input]) => /\/api\/monitors(\?|$)/.test(String(input))).length;

describe("connectSSE()", () => {
  let fetchStub: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    FakeEventSource.instances = [];
    vi.stubGlobal("EventSource", FakeEventSource);
    fetchStub = okFetch();
    vi.stubGlobal("fetch", fetchStub);
    __resetMonitorsForTests();
    __resetToastsForTests();
    __resetSseForTests();
  });

  afterEach(() => {
    __resetSseForTests();
    vi.restoreAllMocks();
  });

  it("opens an EventSource against the configured events URL", () => {
    connectSSE();
    expect(lastSource().url).toContain("/api/monitors/events");
  });

  it("transitions sseStatus on open", () => {
    connectSSE();
    expect(sseStatus()).toBe("connecting");
    lastSource().emitOpen();
    expect(sseStatus()).toBe("open");
  });

  it("merges MonitorChecked into the monitors store", () => {
    __seedMonitorsForTests([buildMonitor({ name: "m1", events: [] })]);
    connectSSE();
    lastSource().emit("MonitorChecked", {
      n: "m1",
      at: 100,
      st: MonitorState.Down,
      ms: 250,
      ns: { lastState: MonitorState.Down, availability: 0 },
    });

    expect(monitors.byName.m1.events[0]).toMatchObject({ at: 100, st: MonitorState.Down, ms: 250 });
    expect(monitors.byName.m1.stats).toMatchObject({ lastState: MonitorState.Down });
  });

  it("fires a toast on MonitorStateChanged", () => {
    __seedMonitorsForTests([buildMonitor({ name: "m1" })]);
    connectSSE();
    lastSource().emit("MonitorStateChanged", {
      n: "m1",
      at: 100,
      st: MonitorState.Down,
    });

    expect(toastList()).toHaveLength(1);
    expect(toastList()[0]).toMatchObject({ title: "m1", variant: "down" });
  });

  it("closes the underlying source when the cleanup is called", () => {
    const dispose = connectSSE();
    const es = lastSource();
    expect(es.readyState).toBe(FakeEventSource.CONNECTING);
    dispose();
    expect(es.readyState).toBe(FakeEventSource.CLOSED);
  });

  describe("resync after reconnect (#18)", () => {
    it("does not refetch on the first open", () => {
      connectSSE();
      lastSource().emitOpen();
      expect(monitorsFetches(fetchStub)).toBe(0);
    });

    it("refetches /api/monitors exactly once when the browser reconnects", () => {
      connectSSE();
      const es = lastSource();
      es.emitOpen();

      // Browser-driven retry: error while CONNECTING, then open again on the same source.
      es.readyState = FakeEventSource.CONNECTING;
      es.emit("error");
      expect(sseStatus()).toBe("connecting");
      es.emitOpen();

      expect(sseStatus()).toBe("open");
      expect(monitorsFetches(fetchStub)).toBe(1);
    });

    it("refetches once per reconnect, not once per event", async () => {
      connectSSE();
      const es = lastSource();
      es.emitOpen();
      es.emit("error");
      es.emitOpen();
      // Solid coalesces refetch() calls issued in the same microtask; a real
      // second reconnect is always at least a tick later.
      await Promise.resolve();
      es.emit("error");
      es.emitOpen();
      expect(monitorsFetches(fetchStub)).toBe(2);
    });
  });

  describe("fatal error state (#18)", () => {
    it("sets status to error when the source is CLOSED and lets connectSSE() reconnect", () => {
      connectSSE();
      const first = lastSource();
      first.emitOpen();

      first.readyState = FakeEventSource.CLOSED;
      first.emit("error");
      expect(sseStatus()).toBe("error");

      // Before the fix activeSource still pointed at the dead source and this was a no-op.
      connectSSE();
      expect(FakeEventSource.instances).toHaveLength(2);
      expect(sseStatus()).toBe("connecting");
    });

    it("treats the open of the replacement source as a reconnect and resyncs", () => {
      connectSSE();
      const first = lastSource();
      first.emitOpen();
      first.readyState = FakeEventSource.CLOSED;
      first.emit("error");

      connectSSE();
      lastSource().emitOpen();

      expect(sseStatus()).toBe("open");
      expect(monitorsFetches(fetchStub)).toBe(1);
    });

    it("reconnects when the tab becomes visible after a fatal error", () => {
      connectSSE();
      const first = lastSource();
      first.emitOpen();
      first.readyState = FakeEventSource.CLOSED;
      first.emit("error");
      expect(sseStatus()).toBe("error");

      vi.spyOn(document, "visibilityState", "get").mockReturnValue("visible");
      document.dispatchEvent(new Event("visibilitychange"));

      expect(FakeEventSource.instances).toHaveLength(2);
      expect(sseStatus()).toBe("connecting");
    });

    it("does not reconnect on visibility change while the browser is still retrying", () => {
      connectSSE();
      const es = lastSource();
      es.emitOpen();
      es.emit("error"); // readyState stays OPEN/CONNECTING: browser handles it
      expect(sseStatus()).toBe("connecting");

      vi.spyOn(document, "visibilityState", "get").mockReturnValue("visible");
      document.dispatchEvent(new Event("visibilitychange"));

      expect(FakeEventSource.instances).toHaveLength(1);
    });
  });

  it("ignores a malformed payload and keeps listening", () => {
    __seedMonitorsForTests([buildMonitor({ name: "m1", events: [] })]);
    connectSSE();
    const es = lastSource();

    expect(() => { es.emit("MonitorChecked", "{not json"); }).not.toThrow();
    expect(() => { es.emit("MonitorStateChanged", "{not json"); }).not.toThrow();
    expect(toastList()).toHaveLength(0);

    es.emit("MonitorChecked", {
      n: "m1",
      at: 100,
      fr: MonitorState.Up,
      st: MonitorState.Down,
      ms: 250,
      ns: { lastState: MonitorState.Down, availability: 0 },
    });
    expect(monitors.byName.m1.events[0]).toMatchObject({ at: 100, st: MonitorState.Down });
  });
});
