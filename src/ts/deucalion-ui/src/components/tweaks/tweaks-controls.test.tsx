import { cleanup, fireEvent, render } from "@solidjs/testing-library";
import { afterEach, describe, expect, it, vi } from "vitest";

import { TweakRadio, TweakSelect, TweakSlider } from "./tweaks-controls";

const OPTIONS = [
  { value: "a", label: "A" },
  { value: "b", label: "B" },
  { value: "c", label: "C" },
  { value: "d", label: "D" },
];

// jsdom has no layout, so getBoundingClientRect returns all zeros. Stub a
// 404px-wide track: with the 2px inset on each side the usable width is 400px,
// giving four 100px segments starting at x=2.
const stubTrack = (container: HTMLElement): void => {
  const track = container.querySelector<HTMLElement>(".twk-seg");
  if (!track) throw new Error("track not rendered");
  track.getBoundingClientRect = (): DOMRect =>
    ({ left: 0, top: 0, width: 404, height: 24, right: 404, bottom: 24, x: 0, y: 0, toJSON: () => ({}) });
};

describe("<TweakSlider />", () => {
  afterEach(cleanup);

  it("shows the current value with its unit", () => {
    const { container } = render(() => (
      <TweakSlider label="Size" value={14} unit="px" onChange={() => { /* noop */ }} />
    ));

    expect(container.querySelector(".twk-val")?.textContent).toBe("14px");
  });

  it("omits the unit when none is given", () => {
    const { container } = render(() => (
      <TweakSlider label="Count" value={3} onChange={() => { /* noop */ }} />
    ));

    expect(container.querySelector(".twk-val")?.textContent).toBe("3");
  });

  it("applies the given bounds to the underlying range input", () => {
    const { container } = render(() => (
      <TweakSlider label="Size" value={5} min={2} max={8} step={2} onChange={() => { /* noop */ }} />
    ));

    const input = container.querySelector<HTMLInputElement>("input[type=range]");
    expect(input?.min).toBe("2");
    expect(input?.max).toBe("8");
    expect(input?.step).toBe("2");
  });

  it("reports the new value as a number, not a string", () => {
    const onChange = vi.fn();
    const { container } = render(() => (
      <TweakSlider label="Size" value={14} onChange={onChange} />
    ));

    const input = container.querySelector<HTMLInputElement>("input[type=range]")!;
    fireEvent.input(input, { target: { value: "21" } });

    expect(onChange).toHaveBeenCalledWith(21);
  });
});

describe("<TweakRadio />", () => {
  afterEach(cleanup);

  // jsdom re-serialises calc(): `2 * (100% - 4px) / 4` comes back as
  // `0.5 * (100% - 4px)`. Recover the segment index from that fraction rather
  // than asserting on the exact string.
  const thumbIndex = (container: HTMLElement, segments = OPTIONS.length): number => {
    const left = container.querySelector<HTMLElement>(".twk-seg-thumb")?.style.left ?? "";
    const match = /2px \+ ([\d.]+) \*/.exec(left);
    if (!match) throw new Error(`Unexpected thumb offset: ${left}`);
    return Math.round(Number(match[1]) * segments);
  };

  it("positions the thumb over the selected segment", () => {
    const { container } = render(() => (
      <TweakRadio label="Pick" value="c" options={OPTIONS} onChange={() => { /* noop */ }} />
    ));

    expect(thumbIndex(container)).toBe(2);
    // Width is one segment: the track minus the 2px inset each side, over N.
    // jsdom folds the "/ 4" into a 0.25 multiplier.
    expect(container.querySelector<HTMLElement>(".twk-seg-thumb")?.style.width)
      .toMatch(/(0\.25 \*|\/ 4)/);
  });

  it("falls back to the first segment when the value is unknown", () => {
    const { container } = render(() => (
      <TweakRadio label="Pick" value="nope" options={OPTIONS} onChange={() => { /* noop */ }} />
    ));

    expect(thumbIndex(container)).toBe(0);
  });

  it("tracks the selected index across every option", () => {
    for (const [i, option] of OPTIONS.entries()) {
      const { container, unmount } = render(() => (
        <TweakRadio label="Pick" value={option.value} options={OPTIONS} onChange={() => { /* noop */ }} />
      ));
      expect(thumbIndex(container)).toBe(i);
      unmount();
    }
  });

  it("marks only the selected option as checked", () => {
    const { container } = render(() => (
      <TweakRadio label="Pick" value="b" options={OPTIONS} onChange={() => { /* noop */ }} />
    ));

    const checked = [...container.querySelectorAll("[role=radio]")]
      .map((el) => el.getAttribute("aria-checked"));
    expect(checked).toEqual(["false", "true", "false", "false"]);
  });

  it.each([
    { clientX: 2, expected: "a" },
    { clientX: 101, expected: "a" },
    { clientX: 102, expected: "b" },
    { clientX: 250, expected: "c" },
    { clientX: 380, expected: "d" },
  ])("maps x=$clientX to segment $expected", ({ clientX, expected }) => {
    const onChange = vi.fn();
    const { container } = render(() => (
      <TweakRadio label="Pick" value="a" options={OPTIONS} onChange={onChange} />
    ));
    stubTrack(container);

    fireEvent.pointerDown(container.querySelector(".twk-seg")!, { clientX });

    if (expected === "a") {
      // Already selected -- the component must not fire a redundant change.
      expect(onChange).not.toHaveBeenCalled();
    } else {
      expect(onChange).toHaveBeenCalledWith(expected);
    }
  });

  it("clamps a pointer past either end to the first and last segments", () => {
    const onChange = vi.fn();
    const { container } = render(() => (
      <TweakRadio label="Pick" value="b" options={OPTIONS} onChange={onChange} />
    ));
    stubTrack(container);
    const track = container.querySelector(".twk-seg")!;

    fireEvent.pointerDown(track, { clientX: -500 });
    expect(onChange).toHaveBeenLastCalledWith("a");

    fireEvent.pointerDown(track, { clientX: 9999 });
    expect(onChange).toHaveBeenLastCalledWith("d");
  });

  it("marks the track as dragging while the pointer is down", () => {
    const { container } = render(() => (
      <TweakRadio label="Pick" value="a" options={OPTIONS} onChange={() => { /* noop */ }} />
    ));
    stubTrack(container);
    const track = container.querySelector(".twk-seg")!;

    expect(track.className).toBe("twk-seg");
    fireEvent.pointerDown(track, { clientX: 250 });
    expect(track.className).toBe("twk-seg dragging");

    fireEvent.pointerUp(window);
    expect(track.className).toBe("twk-seg");
  });
});

describe("<TweakSelect />", () => {
  afterEach(cleanup);

  it("renders every option and reports the chosen value", () => {
    const onChange = vi.fn();
    const { container } = render(() => (
      <TweakSelect label="Pick" value="a" options={OPTIONS} onChange={onChange} />
    ));

    const select = container.querySelector<HTMLSelectElement>("select")!;
    expect(select.options).toHaveLength(4);

    fireEvent.change(select, { target: { value: "c" } });
    expect(onChange).toHaveBeenCalledWith("c");
  });
});
