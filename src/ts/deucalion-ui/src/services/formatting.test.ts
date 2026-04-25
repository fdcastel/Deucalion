import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { MonitorState } from "./deucalion-types";
import {
  fmtAgo,
  fmtDur,
  fmtMs,
  formatLastSeen,
  monitorStateToDescription,
  monitorStateToToastVariant,
  stateLabel,
  stateName,
} from "./formatting";

describe("fmtMs", () => {
  it("renders a placeholder for missing values", () => {
    expect(fmtMs(undefined)).toBe("—");
  });

  it("renders milliseconds rounded for sub-second values", () => {
    expect(fmtMs(0)).toBe("0ms");
    expect(fmtMs(45.4)).toBe("45ms");
    expect(fmtMs(999)).toBe("999ms");
  });

  it("renders seconds with two decimals at and above 1000ms", () => {
    expect(fmtMs(1000)).toBe("1.00s");
    expect(fmtMs(1234)).toBe("1.23s");
  });
});

describe("fmtAgo / fmtDur", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-04-25T12:00:00Z"));
  });
  afterEach(() => { vi.useRealTimers(); });

  const nowSec = (): number => Math.floor(Date.now() / 1000);

  it("anchors on current time and bucketises by magnitude", () => {
    expect(fmtAgo(nowSec())).toBe("just now");
    expect(fmtAgo(nowSec() - 30)).toBe("30s ago");
    expect(fmtAgo(nowSec() - 90)).toBe("2m ago");
    expect(fmtAgo(nowSec() - 7200)).toBe("2h ago");
    expect(fmtAgo(nowSec() - 86400 * 3)).toBe("3d ago");
  });

  it("formats durations with the same buckets", () => {
    expect(fmtDur(0)).toBe("0s");
    expect(fmtDur(45)).toBe("45s");
    expect(fmtDur(120)).toBe("2m");
    expect(fmtDur(7200)).toBe("2.0h");
    expect(fmtDur(86400 * 3)).toBe("3.0d");
  });
});

describe("state name/label/variant", () => {
  it("maps every known state to a distinct CSS class", () => {
    expect(stateName(MonitorState.Up)).toBe("up");
    expect(stateName(MonitorState.Down)).toBe("down");
    expect(stateName(MonitorState.Warn)).toBe("warn");
    expect(stateName(MonitorState.Degraded)).toBe("degraded");
    expect(stateName(MonitorState.Unknown)).toBe("unknown");
  });

  it("maps every known state to a human label", () => {
    expect(stateLabel(MonitorState.Up)).toBe("Up");
    expect(stateLabel(MonitorState.Unknown)).toBe("Unknown");
  });

  it("maps states to their toast variants", () => {
    expect(monitorStateToToastVariant(MonitorState.Up)).toBe("up");
    expect(monitorStateToToastVariant(MonitorState.Down)).toBe("down");
    expect(monitorStateToToastVariant(MonitorState.Unknown)).toBe("default");
  });
});

describe("monitorStateToDescription", () => {
  it("provides human-friendly copy for every state", () => {
    expect(monitorStateToDescription(MonitorState.Up)).toMatch(/online/i);
    expect(monitorStateToDescription(MonitorState.Down)).toMatch(/down/i);
    expect(monitorStateToDescription(MonitorState.Unknown)).toMatch(/unknown/i);
  });
});

describe("formatLastSeen", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-04-25T12:00:00Z"));
  });
  afterEach(() => { vi.useRealTimers(); });

  it("returns undefined when stats are absent", () => {
    expect(formatLastSeen(MonitorState.Up, undefined)).toBeUndefined();
  });

  it("describes the last-seen-down for currently-up monitors", () => {
    const stats = {
      lastState: MonitorState.Up,
      lastUpdate: 0,
      availability: 100,
      averageResponseTimeMs: 0,
      lastSeenDown: Math.floor(Date.now() / 1000) - 60,
    };
    const out = formatLastSeen(MonitorState.Up, stats);
    expect(out).toMatch(/^Last seen down /);
  });

  it("describes the last-seen-up for currently-down monitors", () => {
    const stats = {
      lastState: MonitorState.Down,
      lastUpdate: 0,
      availability: 0,
      averageResponseTimeMs: 0,
      lastSeenUp: Math.floor(Date.now() / 1000) - 300,
    };
    const out = formatLastSeen(MonitorState.Down, stats);
    expect(out).toMatch(/^Last seen up /);
  });
});
