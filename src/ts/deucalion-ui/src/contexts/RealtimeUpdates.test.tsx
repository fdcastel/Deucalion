import { Suspense } from "react";
import { act, render, screen, waitFor } from "@testing-library/react";
import { SWRConfig } from "swr";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { MonitorState } from "../services";
import { MonitorsProvider } from "./MonitorsContext";
import { MonitorHubProvider } from "./MonitorHubContext";
import { useMonitors } from "./MonitorsContext";

// Mock EventSource
const eventSourceState = vi.hoisted(() => {
  const listeners = new Map<string, (e: MessageEvent) => void>();
  const close = vi.fn();
  const removeEventListener = vi.fn((event: string) => { listeners.delete(event); });
  return { listeners, close, removeEventListener };
});

class MockEventSource {
  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSED = 2;
  readyState = MockEventSource.CONNECTING;
  close = eventSourceState.close;
  removeEventListener = eventSourceState.removeEventListener;
  addEventListener(event: string, handler: (e: MessageEvent) => void) {
    eventSourceState.listeners.set(event, handler);
  }
}
vi.stubGlobal("EventSource", MockEventSource);

vi.mock("@heroui/react", () => {
  return {
    toast: vi.fn(),
  };
});

const MonitorProbe = () => {
  const { monitorsData } = useMonitors();
  const apiMonitor = monitorsData?.get("api");

  return (
    <>
      <div data-testid="event-count">{apiMonitor?.events.length ?? 0}</div>
      <div data-testid="latest-event">{apiMonitor?.events[0]?.at ?? 0}</div>
      <div data-testid="last-state">{apiMonitor?.stats?.lastState ?? 0}</div>
    </>
  );
};

describe("Realtime monitor updates", () => {
  beforeEach(() => {
    eventSourceState.listeners.clear();

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => [
          {
            name: "api",
            config: { group: "core" },
            events: [{ at: 10, st: MonitorState.Up }],
            stats: {
              lastState: MonitorState.Up,
              lastUpdate: 10,
              availability: 100,
              averageResponseTimeMs: 10,
            },
          },
        ],
      })
    );
  });

  it("applies MonitorChecked events to rendered monitor state", async () => {
    await act(async () => {
      render(
        <SWRConfig value={{ provider: () => new Map() }}>
          <Suspense fallback={<div>loading</div>}>
            <MonitorsProvider>
              <MonitorHubProvider>
                <MonitorProbe />
              </MonitorHubProvider>
            </MonitorsProvider>
          </Suspense>
        </SWRConfig>
      );
    });

    await waitFor(() => expect(screen.getByTestId("event-count")).toHaveTextContent("1"));
    expect(screen.getByTestId("latest-event")).toHaveTextContent("10");

    const monitorChecked = eventSourceState.listeners.get("MonitorChecked");
    monitorChecked?.(new MessageEvent("MonitorChecked", {
      data: JSON.stringify({
        n: "api",
        at: 11,
        st: MonitorState.Down,
        ns: {
          lastState: MonitorState.Down,
          lastUpdate: 11,
          availability: 90,
          averageResponseTimeMs: 20,
        },
      }),
    }));

    await waitFor(() => expect(screen.getByTestId("event-count")).toHaveTextContent("2"));
    expect(screen.getByTestId("latest-event")).toHaveTextContent("11");
    expect(screen.getByTestId("last-state")).toHaveTextContent(`${MonitorState.Down}`);
  });
});
