import { render } from "@solidjs/testing-library";
import { describe, expect, it } from "vitest";

import { Sparkline } from "./sparkline";

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
});
