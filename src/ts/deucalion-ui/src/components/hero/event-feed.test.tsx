import { render } from "@solidjs/testing-library";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { __resetEventsForTests, onMonitorChecked } from "../../stores/events-store";
import { MonitorState } from "../../services/deucalion-types";

import { EventFeed } from "./event-feed";

describe("<EventFeed>", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-04-25T12:00:00Z"));
    __resetEventsForTests();
  });
  afterEach(() => { vi.useRealTimers(); });

  it("renders an empty list when no events have arrived", () => {
    const { container } = render(() => <EventFeed />);
    expect(container.querySelectorAll(".feed-item")).toHaveLength(0);
  });

  it("renders feed entries for state transitions", () => {
    const nowSec = Math.floor(Date.now() / 1000);
    onMonitorChecked({
      n: "api",
      at: nowSec,
      fr: MonitorState.Up,
      st: MonitorState.Down,
      ms: 250,
      ns: { lastState: MonitorState.Down, lastUpdate: nowSec, availability: 0, averageResponseTimeMs: 250 },
    });

    const { container } = render(() => <EventFeed />);
    const items = container.querySelectorAll(".feed-item");
    expect(items).toHaveLength(1);
    const text = items[0].textContent ?? "";
    expect(text).toContain("api");
    expect(text.toLowerCase()).toContain("up");
    expect(text.toLowerCase()).toContain("down");
    expect(text).toContain("250ms");
  });

  it("limits the visible list to the seven most recent entries", () => {
    for (let i = 0; i < 12; i++) {
      const from = i % 2 === 0 ? MonitorState.Up : MonitorState.Down;
      const to = i % 2 === 0 ? MonitorState.Down : MonitorState.Up;
      onMonitorChecked({
        n: `m${i.toString()}`,
        at: 1000 + i,
        fr: from,
        st: to,
        ns: { lastState: to, lastUpdate: 1000 + i, availability: 100, averageResponseTimeMs: 0 },
      });
    }
    const { container } = render(() => <EventFeed />);
    expect(container.querySelectorAll(".feed-item")).toHaveLength(7);
  });
});
