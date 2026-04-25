import { beforeEach, describe, expect, it } from "vitest";

import { MonitorState, type MonitorCheckedDto, type MonitorStateChangedDto } from "../services/deucalion-types";

import {
  __resetEventsForTests,
  feedEvents,
  onMonitorChecked,
  onMonitorStateChanged,
} from "./events-store";

const checked = (over: Partial<MonitorCheckedDto> = {}): MonitorCheckedDto => ({
  n: "m1",
  at: 1_700_000_001,
  fr: MonitorState.Up,
  st: MonitorState.Warn,
  ms: 100,
  ns: {
    lastState: MonitorState.Warn,
    lastUpdate: 0,
    availability: 100,
    averageResponseTimeMs: 0,
  },
  ...over,
});

const stateChange = (over: Partial<MonitorStateChangedDto> = {}): MonitorStateChangedDto => ({
  n: "m1",
  at: 1_700_000_001,
  fr: MonitorState.Up,
  st: MonitorState.Warn,
  ...over,
});

describe("events-store", () => {
  beforeEach(() => { __resetEventsForTests(); });

  it("ignores MonitorChecked events that don't represent a state transition", () => {
    onMonitorChecked(checked({ fr: MonitorState.Up, st: MonitorState.Up }));
    expect(feedEvents.items).toHaveLength(0);
  });

  it("pushes a feed entry on a state transition", () => {
    onMonitorChecked(checked({ fr: MonitorState.Up, st: MonitorState.Down, ms: 250 }));
    expect(feedEvents.items).toHaveLength(1);
    expect(feedEvents.items[0]).toMatchObject({
      name: "m1",
      from: MonitorState.Up,
      to: MonitorState.Down,
      ms: 250,
    });
  });

  it("orders entries newest-first", () => {
    onMonitorChecked(checked({ at: 100, fr: MonitorState.Up, st: MonitorState.Down }));
    onMonitorChecked(checked({ at: 200, fr: MonitorState.Down, st: MonitorState.Up }));
    expect(feedEvents.items[0].at).toBe(200);
    expect(feedEvents.items[1].at).toBe(100);
  });

  it("caps the buffer at 40 entries", () => {
    for (let i = 0; i < 60; i++) {
      const from = i % 2 === 0 ? MonitorState.Up : MonitorState.Down;
      const to = i % 2 === 0 ? MonitorState.Down : MonitorState.Up;
      onMonitorChecked(checked({ at: 1000 + i, fr: from, st: to }));
    }
    expect(feedEvents.items).toHaveLength(40);
  });

  it("de-dupes a MonitorStateChanged that pairs with an already-recorded check", () => {
    onMonitorChecked(checked({ at: 100, fr: MonitorState.Up, st: MonitorState.Down }));
    onMonitorStateChanged(stateChange({ at: 100, fr: MonitorState.Up, st: MonitorState.Down }));
    expect(feedEvents.items).toHaveLength(1);
  });

  it("records a MonitorStateChanged that arrives without a matching check", () => {
    onMonitorStateChanged(stateChange({ at: 999, fr: MonitorState.Up, st: MonitorState.Down }));
    expect(feedEvents.items).toHaveLength(1);
    expect(feedEvents.items[0].at).toBe(999);
  });
});
