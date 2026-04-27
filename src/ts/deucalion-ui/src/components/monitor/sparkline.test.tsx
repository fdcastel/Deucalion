import { render } from "@solidjs/testing-library";
import { describe, expect, it } from "vitest";

import { Sparkline } from "./sparkline";

// The viewBox is 0..100 with pad=4 on each side; innerH spans y=4 (top) to y=96 (bottom).
const TOP_Y = 4;
const BOTTOM_Y = 96;
const MID_Y = (TOP_Y + BOTTOM_Y) / 2;

function pathYs(d: string): number[] {
  // Path looks like "M4.0 96.0 L48.0 86.8 L92.0 96.0".
  const ys: number[] = [];
  for (const m of d.matchAll(/[ML]\s*[\d.]+\s+([\d.]+)/g)) {
    ys.push(Number(m[1]));
  }
  return ys;
}

describe("<Sparkline>", () => {
  it("renders nothing visible when there are fewer than two points", () => {
    const { container } = render(() => <Sparkline values={[]} />);
    const svg = container.querySelector("svg.spark");
    expect(svg).not.toBeNull();
    // No path elements when there's no data to draw.
    expect(svg!.querySelector("path")).toBeNull();
  });

  it("renders the line and fill paths plus a trailing dot for >=2 points", () => {
    const { container } = render(() => <Sparkline values={[10, 20, 5, 15]} />);
    const svg = container.querySelector("svg.spark")!;
    expect(svg.querySelector("path.spark-line")).not.toBeNull();
    expect(svg.querySelector("path.spark-fill")).not.toBeNull();
    expect(svg.querySelector("circle.spark-dot")).not.toBeNull();
  });

  it("hides the trailing dot when showDot is false", () => {
    const { container } = render(() => <Sparkline values={[10, 20]} showDot={false} />);
    expect(container.querySelector("circle.spark-dot")).toBeNull();
  });

  it("with max set, keeps tight values pinned near the bottom", () => {
    // 1ms of variance against a 1000ms WARN ceiling should look flat near the floor.
    const { container } = render(() => <Sparkline values={[1, 0, 1, 0, 1]} max={1000} />);
    const line = container.querySelector("path.spark-line")!.getAttribute("d")!;
    const ys = pathYs(line);
    // Every point should be far below the midline (i.e. near the bottom).
    for (const y of ys) {
      expect(y).toBeGreaterThan(MID_Y);
    }
  });

  it("with max set, clamps values above the ceiling to the top", () => {
    const { container } = render(() => <Sparkline values={[10, 9999, 10]} max={20} />);
    const line = container.querySelector("path.spark-line")!.getAttribute("d")!;
    const ys = pathYs(line);
    // The clamped peak should sit at the top of the inner area, not above it.
    expect(Math.min(...ys)).toBe(TOP_Y);
  });

  it("renders a faint WARN reference line at the chart top when max is set", () => {
    const { container } = render(() => <Sparkline values={[1, 2, 1]} max={1000} />);
    const warn = container.querySelector("line.spark-warn");
    expect(warn).not.toBeNull();
    expect(Number(warn!.getAttribute("y1"))).toBe(TOP_Y);
    expect(Number(warn!.getAttribute("y2"))).toBe(TOP_Y);
  });

  it("places the WARN line below the chart top when max is below the noise floor", () => {
    // max=2ms gets lifted to a 5ms ceiling; the WARN reference should still
    // mark the actual 2ms threshold inside the chart, not the lifted ceiling.
    const { container } = render(() => <Sparkline values={[0, 1, 0]} max={2} />);
    const warn = container.querySelector("line.spark-warn")!;
    const y = Number(warn.getAttribute("y1"));
    expect(y).toBeGreaterThan(TOP_Y);
    expect(y).toBeLessThan(BOTTOM_Y);
  });

  it("does not render a WARN line in the auto-scale fallback (no max)", () => {
    const { container } = render(() => <Sparkline values={[1, 2, 3]} />);
    expect(container.querySelector("line.spark-warn")).toBeNull();
  });

  it("auto-scale fallback applies a noise floor for tight oscillations", () => {
    // 0/1ms jitter without a max should NOT fill the whole box.
    const { container } = render(() => <Sparkline values={[0, 1, 0, 1, 0, 1]} />);
    const line = container.querySelector("path.spark-line")!.getAttribute("d")!;
    const ys = pathYs(line);
    // Without the floor this would oscillate from y=4 to y=96. With a 5ms floor
    // a 1ms swing should occupy roughly 1/5 of the height — well shy of the top.
    expect(Math.min(...ys)).toBeGreaterThan(MID_Y);
  });
});
