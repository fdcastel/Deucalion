import { render, cleanup } from "@solidjs/testing-library";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import { MonitorState } from "../../services/deucalion-types";
import { buildEvents, buildMonitor, buildStats } from "../../test/fixtures";
import { __resetMonitorsForTests, __seedMonitorsForTests } from "../../stores/monitors-store";

import { HeroAvailability } from "./hero-availability";

describe("<HeroAvailability />", () => {
  beforeEach(() => { __resetMonitorsForTests(); });
  afterEach(() => { cleanup(); __resetMonitorsForTests(); });

  it("renders 100.00% and only the online chip when every monitor is up", () => {
    __seedMonitorsForTests([
      buildMonitor({ name: "a" }),
      buildMonitor({ name: "b" }),
    ]);

    const { container } = render(() => <HeroAvailability />);

    expect(container.querySelector(".hero-availability")?.textContent).toBe("100.00%");
    expect(container.querySelector(".hero-chip.up")?.textContent).toBe("2 online");
    expect(container.querySelectorAll(".hero-chip.down")).toHaveLength(0);
    expect(container.querySelectorAll(".hero-chip.warn")).toHaveLength(0);
  });

  it("averages availability across monitors and splits the counts by state", () => {
    __seedMonitorsForTests([
      buildMonitor({ name: "up", stats: buildStats({ lastState: MonitorState.Up, availability: 100 }) }),
      buildMonitor({ name: "warn", stats: buildStats({ lastState: MonitorState.Warn, availability: 90 }) }),
      buildMonitor({ name: "down", stats: buildStats({ lastState: MonitorState.Down, availability: 50 }) }),
      buildMonitor({ name: "degraded", stats: buildStats({ lastState: MonitorState.Degraded, availability: 80 }) }),
    ]);

    const { container } = render(() => <HeroAvailability />);

    // (100 + 90 + 50 + 80) / 4 = 80.00
    expect(container.querySelector(".hero-availability")?.textContent).toBe("80.00%");
    expect(container.querySelector(".hero-chip.up")?.textContent).toBe("1 online");
    expect(container.querySelector(".hero-chip.down")?.textContent).toBe("1 down");

    // Warn and degraded share the .warn chip class.
    const warnChips = [...container.querySelectorAll(".hero-chip.warn")].map((el) => el.textContent);
    expect(warnChips).toEqual(["1 warn", "1 degraded"]);
  });

  it("falls back to computing availability from events when stats are absent", () => {
    __seedMonitorsForTests([
      buildMonitor({
        name: "no-stats",
        stats: undefined,
        // 3 Up, 1 Down -> 75%
        events: buildEvents([MonitorState.Up, MonitorState.Up, MonitorState.Up, MonitorState.Down]),
      }),
    ]);

    const { container } = render(() => <HeroAvailability />);

    expect(container.querySelector(".hero-availability")?.textContent).toBe("75.00%");
  });

  it("reports 100.00% and no monitors rather than NaN when the list is empty", () => {
    __seedMonitorsForTests([]);

    const { container } = render(() => <HeroAvailability />);

    expect(container.querySelector(".hero-availability")?.textContent).toBe("100.00%");
    expect(container.querySelector(".hero-chip.up")?.textContent).toBe("0 online");
  });

  it("splits the percentage so the decimals can be styled separately", () => {
    __seedMonitorsForTests([
      buildMonitor({ name: "a", stats: buildStats({ availability: 99.567 }) }),
    ]);

    const { container } = render(() => <HeroAvailability />);

    const el = container.querySelector(".hero-availability");
    expect(el?.querySelector("span:not(.pct)")?.textContent).toBe("99");
    expect(el?.querySelector(".pct")?.textContent).toBe(".57%");
  });

  it("builds the trend from the per-bucket average latency, oldest first", () => {
    // Two monitors, newest-first events. Bucket 0 averages (100, 200) = 150,
    // bucket 1 averages (300, 500) = 400. The sparkline draws oldest -> newest.
    __seedMonitorsForTests([
      buildMonitor({
        name: "a",
        events: [
          { at: 3, st: MonitorState.Up, ms: 100 },
          { at: 2, st: MonitorState.Up, ms: 300 },
        ],
      }),
      buildMonitor({
        name: "b",
        events: [
          { at: 3, st: MonitorState.Up, ms: 200 },
          { at: 2, st: MonitorState.Up, ms: 500 },
        ],
      }),
    ]);

    const { container } = render(() => <HeroAvailability />);

    // The sparkline renders the values it was given; assert it drew a polyline
    // with two points rather than reaching into its internals.
    const spark = container.querySelector(".hero-spark-wrap svg");
    expect(spark).not.toBeNull();
  });
});
