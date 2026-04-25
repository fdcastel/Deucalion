import { render } from "@solidjs/testing-library";
import { describe, expect, it } from "vitest";

import { buildEvent, buildEvents } from "../../test/fixtures";
import { MonitorState } from "../../services/deucalion-types";

import { HeartbeatStrip } from "./heartbeat-strip";

const STRIP_LEN = 60;

describe("<HeartbeatStrip>", () => {
  it("always renders 60 ticks", () => {
    const { container } = render(() => <HeartbeatStrip events={[]} />);
    expect(container.querySelectorAll(".tick")).toHaveLength(STRIP_LEN);
  });

  it("pads the left with unknown ticks when fewer than 60 events are present", () => {
    const events = buildEvents([MonitorState.Up, MonitorState.Up, MonitorState.Up]);
    const { container } = render(() => <HeartbeatStrip events={events} />);
    const ticks = container.querySelectorAll(".tick");
    expect(ticks).toHaveLength(STRIP_LEN);
    expect(ticks[0]).toHaveClass("unknown");
    // last 3 ticks reflect the events
    expect(ticks[STRIP_LEN - 1]).toHaveClass("up");
    expect(ticks[STRIP_LEN - 2]).toHaveClass("up");
    expect(ticks[STRIP_LEN - 3]).toHaveClass("up");
  });

  it("colours each tick by the corresponding state", () => {
    const events = [
      buildEvent({ at: 30, st: MonitorState.Down }),
      buildEvent({ at: 20, st: MonitorState.Warn }),
      buildEvent({ at: 10, st: MonitorState.Up }),
    ];
    const { container } = render(() => <HeartbeatStrip events={events} />);
    const ticks = container.querySelectorAll(".tick");
    // events newest-first; rendered oldest→newest left-to-right
    expect(ticks[STRIP_LEN - 3]).toHaveClass("up");
    expect(ticks[STRIP_LEN - 2]).toHaveClass("warn");
    expect(ticks[STRIP_LEN - 1]).toHaveClass("down");
  });
});
