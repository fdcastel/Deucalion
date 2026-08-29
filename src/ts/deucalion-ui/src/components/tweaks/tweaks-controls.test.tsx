import { cleanup, fireEvent, render } from "@solidjs/testing-library";
import { afterEach, describe, expect, it, vi } from "vitest";

import { TweakSelect, TweakSlider } from "./tweaks-controls";

const OPTIONS = [
  { value: "a", label: "A" },
  { value: "b", label: "B" },
  { value: "c", label: "C" },
  { value: "d", label: "D" },
];

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
