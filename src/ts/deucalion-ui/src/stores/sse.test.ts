import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { buildMonitor } from "../test/fixtures";
import { MonitorState } from "../services/deucalion-types";

import { __resetEventsForTests, feedEvents } from "./events-store";
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

describe("connectSSE()", () => {
  beforeEach(() => {
    FakeEventSource.instances = [];
    vi.stubGlobal("EventSource", FakeEventSource);
    __resetMonitorsForTests();
    __resetEventsForTests();
    __resetToastsForTests();
    __resetSseForTests();
  });

  afterEach(() => {
    __resetSseForTests();
    vi.unstubAllGlobals();
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

  it("merges MonitorChecked into the monitors store and the event feed", () => {
    __seedMonitorsForTests([buildMonitor({ name: "m1", events: [] })]);
    connectSSE();
    lastSource().emit("MonitorChecked", {
      n: "m1",
      at: 100,
      fr: MonitorState.Up,
      st: MonitorState.Down,
      ms: 250,
      ns: { lastState: MonitorState.Down, lastUpdate: 100, availability: 0, averageResponseTimeMs: 250 },
    });

    expect(monitors.byName.m1.events[0]).toMatchObject({ at: 100, st: MonitorState.Down, ms: 250 });
    expect(feedEvents.items).toHaveLength(1);
    expect(feedEvents.items[0]).toMatchObject({ name: "m1", from: MonitorState.Up, to: MonitorState.Down });
  });

  it("fires a toast on MonitorStateChanged", () => {
    __seedMonitorsForTests([buildMonitor({ name: "m1" })]);
    connectSSE();
    lastSource().emit("MonitorStateChanged", {
      n: "m1",
      at: 100,
      fr: MonitorState.Up,
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
});
