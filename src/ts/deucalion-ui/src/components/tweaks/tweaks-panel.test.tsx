import { fireEvent, render } from "@solidjs/testing-library";
import { beforeEach, describe, expect, it } from "vitest";

import { __resetTweaksForTests, tweaks } from "../../stores/tweaks-store";

import { TweaksPanel } from "./tweaks-panel";

describe("<TweaksPanel>", () => {
  beforeEach(() => { __resetTweaksForTests(); });

  it("does not render any panel markup when closed", () => {
    const { container } = render(() => <TweaksPanel />);
    expect(container.querySelector(".twk-panel")).toBeNull();
  });

  it("renders the panel only when panelOpen is true", () => {
    render(() => <TweaksPanel />);
    expect(document.querySelector(".twk-panel")).toBeNull();

    tweaks.setPanelOpen(true);

    expect(document.querySelector(".twk-panel")).not.toBeNull();
  });

  it("closes when the X button is clicked", () => {
    tweaks.setPanelOpen(true);
    render(() => <TweaksPanel />);
    const closeBtn = document.querySelector('button[aria-label="Close tweaks"]');
    expect(closeBtn).not.toBeNull();

    fireEvent.click(closeBtn!);

    expect(tweaks.panelOpen()).toBe(false);
    expect(document.querySelector(".twk-panel")).toBeNull();
  });

  it("renders Accent + Typography sections (theme lives on the top-bar button)", () => {
    tweaks.setPanelOpen(true);
    render(() => <TweaksPanel />);
    const sections = document.querySelectorAll(".twk-sect");
    const text = [...sections].map((s) => s.textContent).join(" ");
    expect(text).not.toMatch(/Theme/);
    expect(text).toMatch(/Accent/);
    expect(text).toMatch(/Typography/);
  });
});
