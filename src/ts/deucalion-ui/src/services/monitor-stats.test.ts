import { describe, it, expect } from "vitest";

import { buildEvents, buildMonitor, FIXED_NOW } from "../test/fixtures";
import { MonitorState } from "./deucalion-types";
import {
  aggregateAvailability,
  avail,
  avgMs,
  lastIncident,
  minMs,
  percentile,
} from "./monitor-stats";

describe("avail()", () => {
  it("returns 100 for an empty event window", () => {
    expect(avail([])).toBe(100);
  });

  it("returns 100 when no events are Down", () => {
    const events = buildEvents([MonitorState.Up, MonitorState.Up, MonitorState.Warn]);
    expect(avail(events)).toBe(100);
  });

  it("treats Warn and Degraded as available", () => {
    const events = buildEvents([
      MonitorState.Warn,
      MonitorState.Degraded,
      MonitorState.Up,
    ]);
    expect(avail(events)).toBe(100);
  });

  it("computes a percentage from Down events in the window", () => {
    const events = buildEvents([
      MonitorState.Up,
      MonitorState.Down,
      MonitorState.Up,
      MonitorState.Down,
    ]);
    expect(avail(events)).toBe(50);
  });

  it("ignores Unknown events from both numerator and denominator", () => {
    const events = buildEvents([
      MonitorState.Up,
      MonitorState.Unknown,
      MonitorState.Down,
    ]);
    // 1 Up + 1 Down counted; Unknown skipped
    expect(avail(events)).toBe(50);
  });
});

describe("avgMs() / minMs()", () => {
  it("returns undefined when no event has a response time", () => {
    const events = buildEvents([MonitorState.Down, MonitorState.Down], {
      ms: () => undefined,
    });
    expect(avgMs(events)).toBeUndefined();
    expect(minMs(events)).toBeUndefined();
  });

  it("averages only the events that carry an ms value", () => {
    const events = buildEvents([MonitorState.Up, MonitorState.Down, MonitorState.Up], {
      ms: (_i, st) => (st === MonitorState.Down ? undefined : 100),
    });
    expect(avgMs(events)).toBe(100);
    expect(minMs(events)).toBe(100);
  });

  it("finds the minimum across non-null values", () => {
    const events = buildEvents([MonitorState.Up, MonitorState.Up, MonitorState.Up], {
      ms: (i) => [120, 30, 80][i],
    });
    expect(minMs(events)).toBe(30);
    expect(avgMs(events)).toBeCloseTo((120 + 30 + 80) / 3);
  });
});

describe("percentile()", () => {
  it("returns undefined for an empty window", () => {
    expect(percentile([], 0.5)).toBeUndefined();
  });

  it("matches the nearest-rank definition used by the backend", () => {
    const events = buildEvents(
      Array.from<MonitorState>({ length: 10 }).fill(MonitorState.Up),
      { ms: (i) => (i + 1) * 10 }, // 10, 20, …, 100
    );
    expect(percentile(events, 0.5)).toBe(50);
    expect(percentile(events, 0.95)).toBe(100);
    expect(percentile(events, 0.99)).toBe(100);
  });

  it("clamps p<=0 to the minimum and p>1 to the maximum", () => {
    const events = buildEvents([MonitorState.Up, MonitorState.Up, MonitorState.Up], {
      ms: (i) => [10, 20, 30][i],
    });
    expect(percentile(events, 0)).toBe(10);
    expect(percentile(events, 1)).toBe(30);
  });
});

describe("lastIncident()", () => {
  it("returns undefined when there is no Down/Degraded run in the window", () => {
    const events = buildEvents([MonitorState.Up, MonitorState.Warn, MonitorState.Up]);
    expect(lastIncident(events, FIXED_NOW)).toBeUndefined();
  });

  it("walks newest→oldest and groups consecutive Down events", () => {
    // newest at index 0
    const events = buildEvents(
      [
        MonitorState.Up,
        MonitorState.Down, // most recent run starts here at index 1
        MonitorState.Down,
        MonitorState.Down,
        MonitorState.Up,
      ],
      { stepSec: 5 },
    );
    const inc = lastIncident(events, FIXED_NOW);
    expect(inc).toBeDefined();
    expect(inc!.state).toBe(MonitorState.Down);
    // run spans events[3] (start, oldest) → events[1] (end, newest)
    expect(inc!.start).toBe(events[3].at);
    expect(inc!.end).toBe(events[1].at);
    expect(inc!.durationSec).toBe(events[1].at - events[3].at);
    // events[1].at is `FIXED_NOW - 5`, so age is 5 seconds
    expect(inc!.ageSec).toBe(5);
  });

  it("treats Degraded as an incident state", () => {
    const events = buildEvents([MonitorState.Up, MonitorState.Degraded]);
    const inc = lastIncident(events, FIXED_NOW);
    expect(inc).toBeDefined();
    expect(inc!.state).toBe(MonitorState.Degraded);
  });
});

describe("aggregateAvailability()", () => {
  it("uses the backend stats when present", () => {
    const monitors = [
      buildMonitor({ name: "a", stats: { ...buildMonitor().stats!, availability: 100, lastState: MonitorState.Up } }),
      buildMonitor({ name: "b", stats: { ...buildMonitor().stats!, availability: 80, lastState: MonitorState.Warn } }),
    ];
    const agg = aggregateAvailability(monitors);
    expect(agg.weightedAvailability).toBe(90);
    expect(agg.states.up).toBe(1);
    expect(agg.states.warn).toBe(1);
    expect(agg.total).toBe(2);
  });

  it("falls back to event-window availability when stats are missing", () => {
    // events[0] is the newest; making it Down means lastState classifies as Down.
    const m = buildMonitor({
      stats: undefined,
      events: buildEvents([MonitorState.Down, MonitorState.Up]),
    });
    const agg = aggregateAvailability([m]);
    expect(agg.weightedAvailability).toBe(50);
    expect(agg.states.down).toBe(1);
  });

  it("returns 100% on an empty monitor set", () => {
    const agg = aggregateAvailability([]);
    expect(agg.weightedAvailability).toBe(100);
    expect(agg.total).toBe(0);
  });
});
