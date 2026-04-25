import { beforeEach, describe, expect, it } from "vitest";

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

  it("rewrites the dynamic-fonts <style> tag on font change", () => {
    tweaks.setMonoFont("ibmmono");
    const tag = document.getElementById("dynamic-fonts");
    expect(tag).not.toBeNull();
    expect(tag!.textContent).toContain("IBM Plex Mono");
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
