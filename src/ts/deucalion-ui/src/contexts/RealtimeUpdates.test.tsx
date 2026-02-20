import { Suspense } from "react";
import { act, render, screen, waitFor } from "@testing-library/react";
import { SWRConfig } from "swr";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { MonitorState } from "../services";
import { MonitorsProvider } from "./MonitorsContext";
import { MonitorHubProvider } from "./MonitorHubContext";
import { useMonitors } from "./MonitorsContext";

const signalRState = vi.hoisted(() => {
  const handlers = new Map<string, (payload: unknown) => void>();

  const connection = {
    on: vi.fn((event: string, handler: (payload: unknown) => void) => {
      handlers.set(event, handler);
    }),
    off: vi.fn((event: string) => {
      handlers.delete(event);
    }),
    onclose: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    start: vi.fn(async () => undefined),
    stop: vi.fn(async () => undefined),
  };

  return { handlers, connection };
});

vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 3 },
  HubConnectionState: {
    Disconnected: 0,
    Connected: 1,
    Connecting: 2,
    Reconnecting: 3,
  },
  HubConnectionBuilder: class {
    withUrl() {
      return this;
    }

    withAutomaticReconnect() {
      return this;
    }

    configureLogging() {
      return this;
    }

    build() {
      return signalRState.connection;
    }
  },
}));

vi.mock("@heroui/toast", () => {
  return {
    addToast: vi.fn(),
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
    signalRState.handlers.clear();
    signalRState.connection.start.mockClear();
    signalRState.connection.stop.mockClear();

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

    const monitorChecked = signalRState.handlers.get("MonitorChecked");
    monitorChecked?.({
      n: "api",
      at: 11,
      st: MonitorState.Down,
      ns: {
        lastState: MonitorState.Down,
        lastUpdate: 11,
        availability: 90,
        averageResponseTimeMs: 20,
      },
    });

    await waitFor(() => expect(screen.getByTestId("event-count")).toHaveTextContent("2"));
    expect(screen.getByTestId("latest-event")).toHaveTextContent("11");
    expect(screen.getByTestId("last-state")).toHaveTextContent(`${MonitorState.Down}`);
  });
});
