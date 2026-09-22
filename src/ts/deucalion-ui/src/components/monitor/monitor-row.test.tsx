import { render } from "@solidjs/testing-library";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { buildEvents, buildMonitor, buildStats, FIXED_NOW } from "../../test/fixtures";
import { MonitorState } from "../../services/deucalion-types";

import { MonitorRow } from "./monitor-row";

describe("<MonitorRow>", () => {
  // The incident caption measures an ongoing outage against the clock, and
  // the fixtures' timestamps are anchored at FIXED_NOW.
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(FIXED_NOW * 1000);
  });
  afterEach(() => { vi.useRealTimers(); });

  it("renders the monitor name and type badge", () => {
    const monitor = buildMonitor({ name: "ping-google", config: { type: "ping" } });
    const { container, getByText } = render(() => <MonitorRow monitor={monitor} />);

    expect(getByText("ping-google")).toBeInTheDocument();
    const badge = container.querySelector(".type-badge");
    expect(badge).toHaveClass("t-ping");
    expect(badge?.textContent?.toLowerCase()).toBe("ping");
  });

  it("wraps the name in an anchor when config.href is set", () => {
    const monitor = buildMonitor({
      name: "api",
      config: { type: "http", href: "https://example.com" },
    });
    const { container } = render(() => <MonitorRow monitor={monitor} />);
    const link = container.querySelector(".row-name a");
    expect(link).not.toBeNull();
    expect(link).toHaveAttribute("href", "https://example.com");
    expect(link).toHaveAttribute("target", "_blank");
  });

  it("applies the is-down class when the monitor is down", () => {
    const monitor = buildMonitor({
      stats: buildStats({ lastState: MonitorState.Down, availability: 50 }),
      events: buildEvents([MonitorState.Down, MonitorState.Down]),
    });
    const { container } = render(() => <MonitorRow monitor={monitor} />);
    expect(container.querySelector(".row")).toHaveClass("is-down");
    expect(container.querySelector(".avail")).toHaveClass("down");
  });

  it("renders a no-incident hint when the event window is clean", () => {
    const monitor = buildMonitor({
      events: buildEvents([MonitorState.Up, MonitorState.Up, MonitorState.Up]),
    });
    const { getByText } = render(() => <MonitorRow monitor={monitor} />);
    expect(getByText("no incident")).toBeInTheDocument();
  });

  it("summarises the most recent incident when present", () => {
    const monitor = buildMonitor({
      events: buildEvents([
        MonitorState.Up,
        MonitorState.Down,
        MonitorState.Down,
        MonitorState.Up,
      ]),
    });
    const { container } = render(() => <MonitorRow monitor={monitor} />);
    // A past incident, in the past tense: what it was, and when.
    expect(container.querySelector(".last-incident")?.textContent).toBe("down 5s ago");
    expect(container.querySelector(".last-incident")).not.toHaveAttribute("title");
  });

  it("captions an ongoing outage with how long it has lasted, not when the last probe was", () => {
    // The report: monitors at 0.00%, down for days, captioned as if the
    // outage were ten seconds old.
    // The window holds thirty seconds of Down probes; the backend's `since`
    // says three days.
    const monitor = buildMonitor({
      stats: buildStats({ lastState: MonitorState.Down, availability: 0, since: FIXED_NOW - 3 * 86400, sinceIsLowerBound: false }),
      events: buildEvents([MonitorState.Down, MonitorState.Down, MonitorState.Down, MonitorState.Down], { stepSec: 10 }),
    });
    const { container } = render(() => <MonitorRow monitor={monitor} />);
    const caption = container.querySelector(".last-incident");
    expect(caption?.textContent).toBe("down for 3d");
    expect(caption?.getAttribute("title")).toMatch(/^Down since .+(?<! or earlier)$/);
  });

  it("marks the duration as a lower bound when the run predates the stored history", () => {
    const monitor = buildMonitor({
      stats: buildStats({ lastState: MonitorState.Down, availability: 0, since: FIXED_NOW - 12 * 86400, sinceIsLowerBound: true }),
      events: buildEvents([MonitorState.Down, MonitorState.Down], { stepSec: 10 }),
    });
    const { container } = render(() => <MonitorRow monitor={monitor} />);
    const caption = container.querySelector(".last-incident");
    expect(caption?.textContent).toBe("down for 12d+");
    expect(caption?.getAttribute("title")).toMatch(/ or earlier$/);
  });

  it("measures an ongoing outage from the event window when the stats carry no since", () => {
    const monitor = buildMonitor({
      stats: buildStats({ lastState: MonitorState.Down, availability: 0 }),
      events: buildEvents([MonitorState.Down, MonitorState.Down, MonitorState.Down], { stepSec: 10 }),
    });
    const { container } = render(() => <MonitorRow monitor={monitor} />);
    // The run fills the window, so the window can only give a lower bound.
    expect(container.querySelector(".last-incident")?.textContent).toBe("down for 20s+");
  });

  it("prefers backend-computed percentile stats when present", () => {
    const monitor = buildMonitor({
      stats: buildStats({ minResponseTimeMs: 11, latency50Ms: 22, latency95Ms: 33, latency99Ms: 44 }),
    });
    const { container } = render(() => <MonitorRow monitor={monitor} />);
    const stats = container.querySelector(".lat-stats")?.textContent ?? "";
    expect(stats).toContain("11ms");
    expect(stats).toContain("22ms");
    expect(stats).toContain("33ms");
    expect(stats).toContain("44ms");
  });

  it("hides the latency line for all-Down monitors but keeps the WARN reference", () => {
    // Reproduces the ping-ms case: PingMonitor records 0ms timings on
    // synchronous failures. Those events must not feed the latency line.
    // The WARN threshold marker still draws so users keep the context.
    const monitor = buildMonitor({
      stats: buildStats({
        lastState: MonitorState.Down,
        minResponseTimeMs: undefined,
        latency50Ms: undefined,
        latency95Ms: undefined,
        latency99Ms: undefined,
        warnTimeoutMs: 500,
      }),
      events: buildEvents(
        [MonitorState.Down, MonitorState.Down, MonitorState.Down, MonitorState.Down],
        { ms: () => 0 },
      ),
    });
    const { container } = render(() => <MonitorRow monitor={monitor} />);
    expect(container.querySelector("path.spark-line")).toBeNull();
    expect(container.querySelector("circle.spark-dot")).toBeNull();
    expect(container.querySelector("line.spark-warn")).not.toBeNull();
  });
});
