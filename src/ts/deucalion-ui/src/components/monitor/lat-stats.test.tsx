import { render } from "@solidjs/testing-library";
import { describe, expect, it } from "vitest";

import { buildMonitor, buildStats } from "../../test/fixtures";

import { LatStats } from "./lat-stats";

describe("<LatStats>", () => {
  it("renders min/p50/p95/p99 from backend stats", () => {
    const monitor = buildMonitor({
      stats: buildStats({ minResponseTimeMs: 11, latency50Ms: 22, latency95Ms: 33, latency99Ms: 44 }),
    });
    const { container } = render(() => <LatStats monitor={monitor} />);
    const text = container.textContent ?? "";
    expect(text).toContain("11ms");
    expect(text).toContain("22ms");
    expect(text).toContain("33ms");
    expect(text).toContain("44ms");
  });

  it("lays out cells as (p50, min) (p95, warn) (p99, max) and TIMEOUT on its own row", () => {
    const monitor = buildMonitor({
      stats: buildStats({
        minResponseTimeMs: 11,
        latency50Ms: 22,
        latency95Ms: 33,
        latency99Ms: 44,
        warnTimeoutMs: 100,
        timeoutMs: 2000,
      }),
    });
    const { container } = render(() => <LatStats monitor={monitor} />);
    const cells = Array.from(container.querySelectorAll(".lat-stats > span"))
      .map((el) => el.textContent ?? "");
    expect(cells).toEqual([
      "p50", "22ms",
      "min", "11ms",
      "p95", "33ms",
      "warn", "100ms",
      "p99", "44ms",
      "max", "100ms",
      "timeout", "2.00s",
    ]);
  });

  it("highlights warn label and value with the .warn class", () => {
    const monitor = buildMonitor({ stats: buildStats({ warnTimeoutMs: 100 }) });
    const { container } = render(() => <LatStats monitor={monitor} />);
    const warnCells = container.querySelectorAll(".lat-stats .warn");
    expect(warnCells.length).toBe(2);
    expect(Array.from(warnCells).map((el) => el.textContent)).toEqual(["warn", "100ms"]);
  });

  it("highlights the timeout label and value with the .down class", () => {
    const monitor = buildMonitor({ stats: buildStats({ timeoutMs: 2000 }) });
    const { container } = render(() => <LatStats monitor={monitor} />);
    const downCells = container.querySelectorAll(".lat-stats .down");
    expect(downCells.length).toBe(2);
    expect(Array.from(downCells).map((el) => el.textContent)).toEqual(["timeout", "2.00s"]);
  });

  it("places the TIMEOUT label in the right column via col-right-start", () => {
    const monitor = buildMonitor({ stats: buildStats({ timeoutMs: 2000 }) });
    const { container } = render(() => <LatStats monitor={monitor} />);
    const start = container.querySelector(".lat-stats .col-right-start");
    expect(start?.textContent).toBe("timeout");
  });

  it("lifts max to the noise floor when warn is below it", () => {
    const monitor = buildMonitor({ stats: buildStats({ warnTimeoutMs: 2 }) });
    const { container } = render(() => <LatStats monitor={monitor} />);
    const text = container.textContent ?? "";
    // warn shows the configured 2ms, max sits at the 5ms floor.
    expect(text).toContain("2ms");
    expect(text).toContain("5ms");
  });

  it("renders em-dashes for warn/max/timeout when their fields are missing", () => {
    const monitor = buildMonitor({
      stats: buildStats({ warnTimeoutMs: undefined, timeoutMs: undefined }),
    });
    const { container } = render(() => <LatStats monitor={monitor} />);
    const cells = Array.from(container.querySelectorAll(".lat-stats > span"))
      .map((el) => el.textContent ?? "");
    // labels stay so the layout is stable; values fall back to "—".
    expect(cells[7]).toBe("—");   // warn value
    expect(cells[11]).toBe("—");  // max value
    expect(cells[13]).toBe("—");  // timeout value
  });
});
