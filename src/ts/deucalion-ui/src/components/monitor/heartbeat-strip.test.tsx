import { render } from "@solidjs/testing-library";
import { createSignal } from "solid-js";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { buildEvent, buildEvents } from "../../test/fixtures";
import { MonitorState } from "../../services/deucalion-types";
import { fmtTime } from "../../services/formatting";

import { HeartbeatStrip } from "./heartbeat-strip";

// Strip length is viewport-tier'd: <1280 → 60, 1280–1479 → 90, ≥1480 → 120.
// Pin a narrow viewport for these tests so we exercise the 60-tick tier
// (the assertions about padding + colour ordering are tier-agnostic).
const STRIP_LEN = 60;

const setViewport = (width: number): void => {
  Object.defineProperty(window, "innerWidth", { configurable: true, value: width });
};

describe("<HeartbeatStrip>", () => {
  beforeAll(() => {
    setViewport(500);
  });

  afterEach(() => {
    setViewport(500);
    window.dispatchEvent(new Event("resize"));
    vi.useRealTimers();
  });

  it("always renders 60 ticks at narrow viewports", () => {
    const { container } = render(() => <HeartbeatStrip events={[]} />);
    expect(container.querySelectorAll(".tick")).toHaveLength(STRIP_LEN);
  });

  it("pads the left with unknown ticks when fewer than STRIP_LEN events are present", () => {
    const events = buildEvents([MonitorState.Up, MonitorState.Up, MonitorState.Up]);
    const { container } = render(() => <HeartbeatStrip events={events} />);
    const ticks = container.querySelectorAll(".tick");
    expect(ticks).toHaveLength(STRIP_LEN);
    expect(ticks[0]).toHaveClass("unknown");
    // last 3 ticks reflect the events
    expect(ticks[STRIP_LEN - 1]).toHaveClass("up");
    expect(ticks[STRIP_LEN - 2]).toHaveClass("up");
    expect(ticks[STRIP_LEN - 3]).toHaveClass("up");
  });

  it("colours each tick by the corresponding state", () => {
    const events = [
      buildEvent({ at: 30, st: MonitorState.Down }),
      buildEvent({ at: 20, st: MonitorState.Warn }),
      buildEvent({ at: 10, st: MonitorState.Up }),
    ];
    const { container } = render(() => <HeartbeatStrip events={events} />);
    const ticks = container.querySelectorAll(".tick");
    // events newest-first; rendered oldest→newest left-to-right
    expect(ticks[STRIP_LEN - 3]).toHaveClass("up");
    expect(ticks[STRIP_LEN - 2]).toHaveClass("warn");
    expect(ticks[STRIP_LEN - 1]).toHaveClass("down");
  });

  it("keeps the data-tip wire format on the newest tick (read by the e2e wire-contract test)", () => {
    const events = [buildEvent({ at: 1_700_000_000, st: MonitorState.Up, ms: 42 })];
    const { container } = render(() => <HeartbeatStrip events={events} />);
    const newest = container.querySelector(".tick:last-child");
    expect(newest).toHaveAttribute("data-tip", `Up · ${fmtTime(1_700_000_000)} · 42ms`);
  });

  it("shares one resize listener across every strip (issue #27)", () => {
    const addSpy = vi.spyOn(window, "addEventListener");
    for (let i = 0; i < 10; i++) render(() => <HeartbeatStrip events={[]} />);
    const resizeRegistrations = addSpy.mock.calls.filter(([type]) => type === "resize");
    addSpy.mockRestore();
    // The listener is attached lazily on first use, so an earlier test in this
    // file may already have registered it: at most one, never one per strip.
    expect(resizeRegistrations.length).toBeLessThanOrEqual(1);
  });

  it("re-tiers every mounted strip on a single resize event", () => {
    const a = render(() => <HeartbeatStrip events={[]} />);
    const b = render(() => <HeartbeatStrip events={[]} />);
    expect(a.container.querySelectorAll(".tick")).toHaveLength(60);
    expect(b.container.querySelectorAll(".tick")).toHaveLength(60);

    setViewport(1500);
    window.dispatchEvent(new Event("resize"));
    expect(a.container.querySelectorAll(".tick")).toHaveLength(120);
    expect(b.container.querySelectorAll(".tick")).toHaveLength(120);

    setViewport(1300);
    window.dispatchEvent(new Event("resize"));
    expect(a.container.querySelectorAll(".tick")).toHaveLength(90);
    expect(b.container.querySelectorAll(".tick")).toHaveLength(90);
  });

  it("flags the strip container (not each tick) as fresh for ~600ms when a new event lands", () => {
    vi.useFakeTimers();
    const initial = buildEvents([MonitorState.Up]);
    const [events, setEvents] = createSignal(initial);
    const { container } = render(() => <HeartbeatStrip events={events()} />);
    const strip = container.querySelector(".col-strip")!;

    // Initial render counts as a fresh event too.
    expect(strip).toHaveClass("is-fresh");
    vi.advanceTimersByTime(600);
    expect(strip).not.toHaveClass("is-fresh");

    setEvents([buildEvent({ at: 1_700_000_100, st: MonitorState.Down, ms: undefined }), ...initial]);
    expect(strip).toHaveClass("is-fresh");
    // No per-tick class: the CSS targets `.col-strip.is-fresh .tick:last-child`.
    expect(container.querySelectorAll(".tick.fresh")).toHaveLength(0);
    expect(container.querySelector(".tick:last-child")).toHaveClass("down");

    vi.advanceTimersByTime(600);
    expect(strip).not.toHaveClass("is-fresh");
  });

  it("does not re-flag fresh when the events array changes without a newer event", () => {
    vi.useFakeTimers();
    const initial = buildEvents([MonitorState.Up, MonitorState.Up]);
    const [events, setEvents] = createSignal(initial);
    const { container } = render(() => <HeartbeatStrip events={events()} />);
    const strip = container.querySelector(".col-strip")!;
    vi.advanceTimersByTime(600);
    expect(strip).not.toHaveClass("is-fresh");

    setEvents([...initial]); // same newest timestamp, new array identity
    expect(strip).not.toHaveClass("is-fresh");
  });

  it("exposes the newest check to assistive tech and keyboard users", () => {
    const events = [
      buildEvent({ at: 1_700_000_010, st: MonitorState.Warn, ms: 250 }),
      buildEvent({ at: 1_700_000_000, st: MonitorState.Up, ms: 42 }),
    ];
    const { container } = render(() => <HeartbeatStrip events={events} />);
    const strip = container.querySelector(".col-strip")!;
    expect(strip).toHaveAttribute("role", "img");
    expect(strip).toHaveAttribute("tabindex", "0");
    expect(strip).toHaveAttribute(
      "aria-label",
      `Recent check history: 2 checks, latest Warn · ${fmtTime(1_700_000_010)} · 250ms`,
    );
  });

  it("names an empty strip without pretending to have data", () => {
    const { container } = render(() => <HeartbeatStrip events={[]} />);
    expect(container.querySelector(".col-strip")).toHaveAttribute("aria-label", "Recent check history: no checks yet");
  });
});
