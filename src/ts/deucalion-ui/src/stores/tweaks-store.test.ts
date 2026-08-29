import { beforeEach, describe, expect, it, vi } from "vitest";

import { __resetTweaksForTests, tweaks } from "./tweaks-store";

const STORAGE_KEY = "deucalion.tweaks";

describe("tweaks-store", () => {
  beforeEach(() => {
    __resetTweaksForTests();
  });

  it("applies the theme to the documentElement on change", () => {
    tweaks.setTheme("light");
    expect(document.documentElement.getAttribute("data-theme")).toBe("light");

    tweaks.setTheme("dark");
    expect(document.documentElement.getAttribute("data-theme")).toBe("dark");
  });

  it("computes derived accent vars imperatively (not in CSS)", () => {
    tweaks.setTheme("dark");
    tweaks.setAccentHue(220);
    tweaks.setAccentChroma(0.15);

    const root = document.documentElement;
    expect(root.style.getPropertyValue("--accent-h")).toBe("220");
    expect(root.style.getPropertyValue("--accent-c")).toBe("0.15");
    expect(root.style.getPropertyValue("--accent")).toContain("0.15");
    expect(root.style.getPropertyValue("--accent")).toContain("220");
    expect(root.style.getPropertyValue("--flash")).toContain("220");
  });

  it("applies fonts as custom properties on the documentElement (issue #33)", () => {
    const root = document.documentElement;

    tweaks.setMonoFont("spacemono");
    tweaks.setUiFont("system");
    tweaks.setDisplayFont("newsreader");
    expect(root.style.getPropertyValue("--font-mono")).toBe('"Space Mono", ui-monospace, monospace');
    expect(root.style.getPropertyValue("--font-ui")).toContain("system-ui");
    expect(root.style.getPropertyValue("--font-display")).toBe('"Newsreader", Georgia, serif');
    expect(root.style.getPropertyValue("--display-italic")).toBe("italic");

    // A display face that does not italicize flips the token back to normal.
    tweaks.setDisplayFont("ibmsans");
    expect(root.style.getPropertyValue("--font-display")).toBe('"IBM Plex Sans", sans-serif');
    expect(root.style.getPropertyValue("--display-italic")).toBe("normal");
  });

  it("no longer injects a #dynamic-fonts stylesheet (issue #33)", () => {
    tweaks.setMonoFont("ibmmono");
    expect(document.getElementById("dynamic-fonts")).toBeNull();
    expect(document.querySelectorAll("style")).toHaveLength(0);
  });

  it("falls back to a known face for an unknown font key", () => {
    tweaks.setMonoFont("does-not-exist");
    expect(document.documentElement.style.getPropertyValue("--font-mono")).toBe('"JetBrains Mono", ui-monospace, monospace');
  });

  it("persists exactly once per change (single effect, issue #27)", () => {
    const setItem = vi.spyOn(Storage.prototype, "setItem");
    tweaks.setAccentHue(12);
    expect(setItem).toHaveBeenCalledTimes(1);
    tweaks.setMonoFont("spacemono");
    expect(setItem).toHaveBeenCalledTimes(2);
    setItem.mockRestore();
  });

  it("persists every signal to localStorage", () => {
    tweaks.setTheme("light");
    tweaks.setAccentHue(330);

    const raw = localStorage.getItem(STORAGE_KEY);
    expect(raw).not.toBeNull();
    const parsed = JSON.parse(raw!) as { theme: string; accentHue: number };
    expect(parsed.theme).toBe("light");
    expect(parsed.accentHue).toBe(330);
  });

  it("toggles panelOpen", () => {
    expect(tweaks.panelOpen()).toBe(false);
    tweaks.setPanelOpen(true);
    expect(tweaks.panelOpen()).toBe(true);
  });

  it("defaults to the IBM Plex font preset", () => {
    expect(tweaks.displayFont()).toBe("ibmsans");
    expect(tweaks.uiFont()).toBe("ibmsans");
    expect(tweaks.monoFont()).toBe("ibmmono");
  });
});

describe("tweaks-store remote fonts (issue #28)", () => {
  beforeEach(() => {
    __resetTweaksForTests();
  });

  const link = (): HTMLLinkElement | null =>
    document.getElementById("dynamic-google-fonts") as HTMLLinkElement | null;

  it("makes no third-party font request for the default IBM Plex preset", () => {
    expect(link()).toBeNull();
  });

  it("loads a non-hosted face from Google Fonts only when it is selected", () => {
    tweaks.setDisplayFont("newsreader");
    const l = link();
    expect(l).not.toBeNull();
    expect(l!.rel).toBe("stylesheet");
    expect(l!.href).toContain("fonts.googleapis.com/css2?family=Newsreader");
    expect(l!.href).toContain("display=swap");
  });

  it("carries every selected remote family in one link, deduplicated", () => {
    tweaks.setDisplayFont("jetbrains");
    tweaks.setMonoFont("jetbrains");
    tweaks.setUiFont("inter");
    const href = link()!.href;
    expect(href.match(/family=JetBrains\+Mono/g)).toHaveLength(1);
    expect(href).toContain("family=Inter");
    expect(document.querySelectorAll("#dynamic-google-fonts")).toHaveLength(1);
  });

  it("removes the link again once every face is self-hosted", () => {
    tweaks.setDisplayFont("newsreader");
    expect(link()).not.toBeNull();
    tweaks.setDisplayFont("ibmsans");
    expect(link()).toBeNull();
  });
});
