import { render } from "@solidjs/testing-library";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { __resetToastsForTests, showToast } from "../../stores/toast-store";

import { ToastStack } from "./toast";

describe("<ToastStack>", () => {
  beforeEach(() => {
    __resetToastsForTests();
    vi.useFakeTimers();
  });
  afterEach(() => { vi.useRealTimers(); });

  it("renders queued toasts", () => {
    showToast({ title: "First", description: "hi", variant: "up" });
    showToast({ title: "Second", variant: "down" });

    render(() => <ToastStack />);

    const toasts = document.querySelectorAll(".toast");
    expect(toasts).toHaveLength(2);
    expect(toasts[0]).toHaveClass("up");
    expect(toasts[1]).toHaveClass("down");
    expect(document.body.textContent).toContain("First");
    expect(document.body.textContent).toContain("Second");
  });

  it("removes toasts after the TTL elapses", () => {
    showToast({ title: "Auto", variant: "up" });
    render(() => <ToastStack />);
    expect(document.querySelectorAll(".toast")).toHaveLength(1);

    vi.advanceTimersByTime(4001);

    expect(document.querySelectorAll(".toast")).toHaveLength(0);
  });
});
