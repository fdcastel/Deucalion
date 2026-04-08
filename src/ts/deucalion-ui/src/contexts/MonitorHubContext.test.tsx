import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { appendNewEvent, MonitorHubProvider, useMonitorHubContext } from "./MonitorHubContext";
import { MonitorState, MonitorProps } from "../services";

const testDoubles = vi.hoisted(() => ({
  mockedMutateMonitors: vi.fn(),
  addToastMock: vi.fn(),
}));

// Mock EventSource
const eventSourceState = vi.hoisted(() => {
  const listeners = new Map<string, (e: MessageEvent) => void>();
  const close = vi.fn();
  const removeEventListener = vi.fn();
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

vi.mock("@heroui/react", () => ({
  toast: testDoubles.addToastMock,
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
    eventSourceState.listeners.clear();
    eventSourceState.close.mockClear();
  });

  afterEach(() => {
    testDoubles.mockedMutateMonitors.mockReset();
  });

  it("opens SSE connection and handles incoming events", async () => {
    const { unmount } = render(
      <MonitorHubProvider>
        <HubProbe />
      </MonitorHubProvider>
    );

    // Simulate connection open
    const openHandler = eventSourceState.listeners.get("open") as (e: Event) => void;
    openHandler(new Event("open"));

    await waitFor(() => expect(screen.getByTestId("connected")).toHaveTextContent("yes"));

    const monitorChecked = eventSourceState.listeners.get("MonitorChecked") as (e: MessageEvent) => void;
    const monitorChanged = eventSourceState.listeners.get("MonitorStateChanged") as (e: MessageEvent) => void;

    monitorChecked?.(new MessageEvent("MonitorChecked", {
      data: JSON.stringify({
        n: "api",
        at: 10,
        st: MonitorState.Up,
        ns: {
          lastState: MonitorState.Up,
          lastUpdate: 10,
          availability: 100,
          averageResponseTimeMs: 1,
        },
      }),
    }));

    monitorChanged?.(new MessageEvent("MonitorStateChanged", {
      data: JSON.stringify({ n: "api", at: 10, st: MonitorState.Down }),
    }));

    expect(testDoubles.mockedMutateMonitors).toHaveBeenCalled();
    await waitFor(() => expect(testDoubles.addToastMock).toHaveBeenCalled());

    unmount();

    expect(eventSourceState.close).toHaveBeenCalled();
  });
});
