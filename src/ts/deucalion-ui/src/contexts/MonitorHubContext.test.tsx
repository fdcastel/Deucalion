import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { appendNewEvent, MonitorHubProvider, useMonitorHubContext } from "./MonitorHubContext";
import { MonitorState, MonitorProps } from "../services";

const testDoubles = vi.hoisted(() => ({
  mockedMutateMonitors: vi.fn(),
  addToastMock: vi.fn(),
}));

const signalRState = vi.hoisted(() => {
  const handlers = new Map<string, (payload: unknown) => void>();

  const connection = {
    on: vi.fn((event: string, handler: (payload: unknown) => void) => {
      handlers.set(event, handler);
    }),
    off: vi.fn(),
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

vi.mock("@heroui/react", () => ({
  addToast: testDoubles.addToastMock,
}));

vi.mock("./MonitorsContext", () => ({
  useMonitors: () => ({ mutateMonitors: testDoubles.mockedMutateMonitors }),
}));

const HubProbe = () => {
  const { isConnected, isConnecting, connectionError } = useMonitorHubContext();
  return (
    <>
      <div data-testid="connected">{isConnected ? "yes" : "no"}</div>
      <div data-testid="connecting">{isConnecting ? "yes" : "no"}</div>
      <div data-testid="error">{connectionError ? connectionError.message : "none"}</div>
    </>
  );
};

describe("appendNewEvent", () => {
  it("prepends a new event and updates stats", () => {
    const monitors = new Map<string, MonitorProps>([
      [
        "api",
        {
          name: "api",
          config: {},
          events: [{ at: 1, st: MonitorState.Up }],
          stats: {
            lastState: MonitorState.Up,
            lastUpdate: 1,
            availability: 100,
            averageResponseTimeMs: 10,
          },
        },
      ],
    ]);

    const updated = appendNewEvent(monitors, {
      n: "api",
      at: 2,
      st: MonitorState.Down,
      ms: 200,
      te: "Timeout",
      ns: {
        lastState: MonitorState.Down,
        lastUpdate: 2,
        availability: 90,
        averageResponseTimeMs: 20,
      },
    });

    const apiMonitor = updated.get("api");
    expect(apiMonitor?.events[0].at).toBe(2);
    expect(apiMonitor?.stats?.lastState).toBe(MonitorState.Down);
    expect(apiMonitor?.events).toHaveLength(2);
  });
});

describe("MonitorHubProvider", () => {
  beforeEach(() => {
    testDoubles.mockedMutateMonitors.mockClear();
    testDoubles.addToastMock.mockClear();
    signalRState.handlers.clear();
    signalRState.connection.start.mockClear();
    signalRState.connection.stop.mockClear();
  });

  afterEach(() => {
    testDoubles.mockedMutateMonitors.mockReset();
  });

  it("starts SignalR connection and handles incoming hub events", async () => {
    const { unmount } = render(
      <MonitorHubProvider>
        <HubProbe />
      </MonitorHubProvider>
    );

    await waitFor(() => expect(screen.getByTestId("connected")).toHaveTextContent("yes"));

    const monitorChecked = signalRState.handlers.get("MonitorChecked");
    const monitorChanged = signalRState.handlers.get("MonitorStateChanged");

    monitorChecked?.({
      n: "api",
      at: 10,
      st: MonitorState.Up,
      ns: {
        lastState: MonitorState.Up,
        lastUpdate: 10,
        availability: 100,
        averageResponseTimeMs: 1,
      },
    });

    monitorChanged?.({ n: "api", at: 10, st: MonitorState.Down });

    expect(testDoubles.mockedMutateMonitors).toHaveBeenCalled();
    expect(testDoubles.addToastMock).toHaveBeenCalled();

    unmount();

    await waitFor(() => expect(signalRState.connection.stop).toHaveBeenCalled());
  });
});
