import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { MonitorState, type MonitorStateChangedDto } from "../services/deucalion-types";

import {
  __resetToastsForTests,
  showStateChangeToast,
  showToast,
  toastList,
} from "./toast-store";

describe("toast-store", () => {
  beforeEach(() => {
    __resetToastsForTests();
    vi.useFakeTimers();
  });
  afterEach(() => { vi.useRealTimers(); });

  it("appends toasts and assigns sequential ids", () => {
    showToast({ title: "a", variant: "up" });
    showToast({ title: "b", variant: "down" });

    expect(toastList()).toHaveLength(2);
    expect(toastList()[0].id).toBeLessThan(toastList()[1].id);
  });

  it("auto-dismisses after the TTL", () => {
    showToast({ title: "a", variant: "up" });
    expect(toastList()).toHaveLength(1);

    vi.advanceTimersByTime(4001);

    expect(toastList()).toHaveLength(0);
  });

  it("derives toast content from MonitorStateChanged events", () => {
    const event: MonitorStateChangedDto = {
      n: "api-prod",
      at: 0,
      fr: MonitorState.Up,
      st: MonitorState.Down,
    };

    showStateChangeToast(event);

    expect(toastList()).toHaveLength(1);
    expect(toastList()[0]).toMatchObject({
      title: "api-prod",
      variant: "down",
    });
    expect(toastList()[0].description).toMatch(/down/i);
  });
});
