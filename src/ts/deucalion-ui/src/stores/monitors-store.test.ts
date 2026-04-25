import { beforeEach, describe, expect, it } from "vitest";

import { buildEvent, buildEvents, buildMonitor, buildStats } from "../test/fixtures";
import { MonitorState, type MonitorCheckedDto } from "../services/deucalion-types";

import {
  __resetMonitorsForTests,
  __seedMonitorsForTests,
  mergeChecked,
  monitorList,
  monitors,
} from "./monitors-store";

const seed = (over: Parameters<typeof buildMonitor>[0] = {}): void => {
  __seedMonitorsForTests([buildMonitor({ name: "m1", ...over })]);
};

describe("monitors-store", () => {
  beforeEach(() => { __resetMonitorsForTests(); });

  describe("monitorList()", () => {
    it("returns monitors in insertion order", () => {
      __seedMonitorsForTests([
        buildMonitor({ name: "a" }),
        buildMonitor({ name: "b" }),
        buildMonitor({ name: "c" }),
      ]);
      expect(monitorList().map((m) => m.name)).toEqual(["a", "b", "c"]);
    });
  });

  describe("mergeChecked()", () => {
    const checked = (over: Partial<MonitorCheckedDto> = {}): MonitorCheckedDto => ({
      n: "m1",
      at: 1_700_000_001,
      fr: MonitorState.Up,
      st: MonitorState.Warn,
      ms: 100,
      ns: buildStats({ lastState: MonitorState.Warn }),
      ...over,
    });

    it("prepends a new event and updates stats", () => {
      seed({ events: [buildEvent({ at: 1_700_000_000, st: MonitorState.Up, ms: 50 })] });

      mergeChecked(checked());

      const m = monitors.byName.m1;
      expect(m.events).toHaveLength(2);
      expect(m.events[0]).toMatchObject({ at: 1_700_000_001, st: MonitorState.Warn, ms: 100 });
      expect(m.stats?.lastState).toBe(MonitorState.Warn);
    });

    it("caps the rolling window at 60 events", () => {
      seed({ events: buildEvents(Array.from<MonitorState>({ length: 60 }).fill(MonitorState.Up)) });

      mergeChecked(checked({ at: 1_800_000_000 }));

      expect(monitors.byName.m1.events).toHaveLength(60);
      expect(monitors.byName.m1.events[0].at).toBe(1_800_000_000);
    });

    it("ignores events with a timestamp it has already seen", () => {
      const e0 = buildEvent({ at: 1_700_000_000 });
      seed({ events: [e0] });
      const before = monitors.byName.m1.events.length;

      mergeChecked(checked({ at: 1_700_000_000 }));

      expect(monitors.byName.m1.events).toHaveLength(before);
    });

    it("does nothing when the monitor name is unknown", () => {
      __seedMonitorsForTests([]);
      mergeChecked(checked({ n: "ghost" }));
      expect(monitors.byName.ghost).toBeUndefined();
    });
  });
});
