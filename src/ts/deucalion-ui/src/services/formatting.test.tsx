import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { formatMonitorEvent } from "./formatting";
import { MonitorState } from "./deucalion-types";

describe("formatMonitorEvent", () => {
  it("renders response time and response text", () => {
    render(formatMonitorEvent({ at: 1_700_000_000, st: MonitorState.Up, ms: 123, te: "OK" }));

    expect(screen.getByText("123ms", { exact: false })).toBeInTheDocument();
    expect(screen.getByText("OK")).toBeInTheDocument();
  });
});
