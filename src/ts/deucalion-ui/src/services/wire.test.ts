import { describe, expect, it } from "vitest";

import { MonitorState, type MonitorEventsDto } from "./deucalion-types";
import { decodeEvents, decodeMonitor } from "./wire";

describe("decodeEvents (columnar GET /api/monitors events)", () => {
  it("expands the newest timestamp + deltas, the state string and the latency array into events", () => {
    const wire: MonitorEventsDto = { at: 1_787_848_364, dt: [60, 61], st: "231", ms: [118, 900, null] };

    expect(decodeEvents(wire)).toEqual([
      { at: 1_787_848_364, st: MonitorState.Up, ms: 118 },
      { at: 1_787_848_304, st: MonitorState.Warn, ms: 900 },
      { at: 1_787_848_243, st: MonitorState.Down },
    ]);
  });

  it("omits `ms` (rather than writing undefined/null) when the probe recorded none", () => {
    const [down] = decodeEvents({ at: 1, dt: [], st: "1", ms: [null] });
    expect(down).not.toHaveProperty("ms");
  });

  it("returns an empty list when the key is absent (a monitor with no events yet)", () => {
    expect(decodeEvents(undefined)).toEqual([]);
  });

  it("decodes a single event with no deltas", () => {
    expect(decodeEvents({ at: 5, dt: [], st: "2", ms: [7] })).toEqual([{ at: 5, st: MonitorState.Up, ms: 7 }]);
  });

  it("round-trips a 120-event history", () => {
    const events = Array.from({ length: 120 }, (_, i) => ({ at: 2_000_000_000 - i * 60 - (i % 3), st: MonitorState.Up, ms: 10 + i }));
    const wire: MonitorEventsDto = {
      at: events[0].at,
      dt: events.slice(0, -1).map((e, i) => e.at - events[i + 1].at),
      st: events.map((e) => String(e.st)).join(""),
      ms: events.map((e) => e.ms),
    };
    expect(decodeEvents(wire)).toEqual(events);
  });

  it("decodeMonitor keeps every other field and replaces events", () => {
    const monitor = decodeMonitor({ name: "m", config: { type: "tcp" }, events: { at: 9, dt: [], st: "2", ms: [1] } });
    expect(monitor).toEqual({ name: "m", config: { type: "tcp" }, events: [{ at: 9, st: MonitorState.Up, ms: 1 }] });
    expect(decodeMonitor({ name: "n", config: { type: "ping" } }).events).toEqual([]);
  });
});
