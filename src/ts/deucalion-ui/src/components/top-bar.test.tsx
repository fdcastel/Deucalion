import { fireEvent, render } from "@solidjs/testing-library";
import { beforeEach, describe, expect, it } from "vitest";

import { buildMonitor, buildStats } from "../test/fixtures";
import { __resetMonitorsForTests, __seedMonitorsForTests } from "../stores/monitors-store";
import { __resetTweaksForTests, tweaks } from "../stores/tweaks-store";
import { MonitorState } from "../services/deucalion-types";

import { TopBar } from "./top-bar";

describe("<TopBar>", () => {
  beforeEach(() => {
    __resetMonitorsForTests();
    __resetTweaksForTests();
  });

  it("renders the brand mark and the page title", () => {
    __seedMonitorsForTests([buildMonitor({ name: "a" })]);
    const { container } = render(() => <TopBar />);
    expect(container.querySelector(".brand-mark")).not.toBeNull();
    expect(container.querySelector(".brand-name")?.textContent).toBeTruthy();
  });

  it("toggles theme via the theme button", () => {
    __seedMonitorsForTests([]);
    render(() => <TopBar />);
    const themeBtn = document.querySelector('button[aria-label="Toggle theme"]')!;

    expect(tweaks.theme()).toBe("dark");
    fireEvent.click(themeBtn);
    expect(tweaks.theme()).toBe("light");
    fireEvent.click(themeBtn);
    expect(tweaks.theme()).toBe("dark");
  });

  it("toggles the tweaks panel via the discreet trigger", () => {
    __seedMonitorsForTests([]);
    render(() => <TopBar />);
    const trigger = document.querySelector('button[aria-label="Open tweaks panel"]')!;

    expect(tweaks.panelOpen()).toBe(false);
    fireEvent.click(trigger);
    expect(tweaks.panelOpen()).toBe(true);
  });

  it("updates document.title with a (-N) prefix when monitors are down", () => {
    __seedMonitorsForTests([
      buildMonitor({ name: "a", stats: buildStats({ lastState: MonitorState.Down }) }),
      buildMonitor({ name: "b", stats: buildStats({ lastState: MonitorState.Down }) }),
      buildMonitor({ name: "c" }),
    ]);

    render(() => <TopBar />);

    expect(document.title).toMatch(/^\(-2\) /);
  });
});
