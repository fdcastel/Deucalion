import { Suspense } from "react";
import { act, render, screen } from "@testing-library/react";
import { SWRConfig } from "swr";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { MonitorsProvider, useMonitors } from "./MonitorsContext";

const MonitorsProbe = () => {
  const { monitorsData, groupedMonitorsData, usingImages } = useMonitors();
  return (
    <>
      <div data-testid="count">{monitorsData?.size ?? 0}</div>
      <div data-testid="groups">{groupedMonitorsData?.size ?? 0}</div>
      <div data-testid="using-images">{usingImages ? "yes" : "no"}</div>
    </>
  );
};

describe("MonitorsProvider", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ([
        { name: "m1", config: { group: "core", image: "x.png" }, events: [] },
        { name: "m2", config: { group: "core" }, events: [] },
      ]),
    }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders monitors and grouped monitor context values", async () => {
    await act(async () => {
      render(
        <SWRConfig value={{ provider: () => new Map() }}>
          <Suspense fallback={<div>loading</div>}>
            <MonitorsProvider>
              <MonitorsProbe />
            </MonitorsProvider>
          </Suspense>
        </SWRConfig>
      );
    });

    expect(await screen.findByTestId("count")).toHaveTextContent("2");
    expect(screen.getByTestId("groups")).toHaveTextContent("1");
    expect(screen.getByTestId("using-images")).toHaveTextContent("yes");
  });
});
