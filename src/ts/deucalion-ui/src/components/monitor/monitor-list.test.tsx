import { render } from "@solidjs/testing-library";
import { beforeEach, describe, expect, it } from "vitest";

import { buildMonitor } from "../../test/fixtures";
import { __resetMonitorsForTests, __seedMonitorsForTests } from "../../stores/monitors-store";

import { MonitorList } from "./monitor-list";

describe("<MonitorList>", () => {
  beforeEach(() => { __resetMonitorsForTests(); });

  it("renders all monitors when there is exactly one (un-named) group", () => {
    __seedMonitorsForTests([
      buildMonitor({ name: "a" }),
      buildMonitor({ name: "b" }),
    ]);
    const { container } = render(() => <MonitorList />);
    expect(container.querySelectorAll(".row")).toHaveLength(2);
    // No subgroup hairline header when there's only the implicit bucket.
    expect(container.querySelectorAll(".subgroup")).toHaveLength(0);
  });

  it("emits a subgroup hairline label per distinct group", () => {
    __seedMonitorsForTests([
      buildMonitor({ name: "a", config: { type: "http", group: "Edge" } }),
      buildMonitor({ name: "b", config: { type: "http", group: "Edge" } }),
      buildMonitor({ name: "c", config: { type: "ping", group: "Origin" } }),
    ]);
    const { container } = render(() => <MonitorList />);
    const subs = [...container.querySelectorAll(".subgroup")].map((el) => el.textContent);
    expect(subs).toContain("Edge");
    expect(subs).toContain("Origin");
  });

  it("does not render a group-header any more (count + tally moved to the hero)", () => {
    __seedMonitorsForTests([buildMonitor({ name: "a" }), buildMonitor({ name: "b" })]);
    const { container } = render(() => <MonitorList />);
    expect(container.querySelector(".group-header")).toBeNull();
    expect(container.querySelector(".group-title")).toBeNull();
  });
});
