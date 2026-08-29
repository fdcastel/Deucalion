import { createResource } from "solid-js";
import { createStore, produce } from "solid-js/store";

import { API_MONITORS_URL, MAX_EVENT_HISTORY } from "../configuration";
import type {
  MonitorCheckedDto,
  MonitorEventDto,
  MonitorDto,
} from "../services/deucalion-types";
import { fetchWithRetry } from "../services/fetch-with-retry";

interface MonitorsStoreState {
  byName: Record<string, MonitorDto>;
  order: string[];
  loaded: boolean;
}

const [state, setState] = createStore<MonitorsStoreState>({
  byName: {},
  order: [],
  loaded: false,
});

const fetchMonitors = async (): Promise<MonitorDto[]> => {
  const response = await fetchWithRetry(API_MONITORS_URL);
  return await response.json() as MonitorDto[];
};

const [monitorsResource, { refetch }] = createResource(async () => {
  const list = await fetchMonitors();
  setState(
    produce((s) => {
      s.byName = {};
      s.order = [];
      for (const m of list) {
        s.byName[m.name] = m;
        s.order.push(m.name);
      }
      s.loaded = true;
    }),
  );
  return list;
});

// eslint-disable-next-line solid/reactivity
export const monitors = state;

// Consumers that decide whether the app is "ready" must read the resource
// itself, not just `loaded`: Solid stores a resource's rejection and only
// rethrows on read, so a fatal fetch (4xx) that nobody reads is swallowed
// and the splash never goes away (#17).
export { monitorsResource };
export const monitorsLoaded = (): boolean => state.loaded;

// Re-run the initial fetch. Used by the SSE layer after a reconnect: events
// missed while the stream was down would otherwise leave a permanent hole
// in the heartbeat strip and stale stats (#18).
export const refetchMonitors = (): void => { void refetch(); };

export const monitorList = (): MonitorDto[] => state.order.map((name) => state.byName[name]);

// Test-only: replace the in-memory monitor list with a fixed set.
export const __seedMonitorsForTests = (list: MonitorDto[]): void => {
  setState(
    produce((s) => {
      s.byName = {};
      s.order = [];
      for (const m of list) {
        s.byName[m.name] = m;
        s.order.push(m.name);
      }
      s.loaded = true;
    }),
  );
};

export const __resetMonitorsForTests = (): void => {
  setState(
    produce((s) => {
      s.byName = {};
      s.order = [];
      s.loaded = false;
    }),
  );
};

export const mergeChecked = (event: MonitorCheckedDto): void => {
  setState(
    produce((s) => {
      if (!Object.prototype.hasOwnProperty.call(s.byName, event.n)) return;
      const monitor = s.byName[event.n];

      // Already saw an event for this exact timestamp — skip.
      if (monitor.events.some((x) => x.at === event.at)) return;

      const newEvent: MonitorEventDto = {
        at: event.at,
        st: event.st,
        ms: event.ms,
      };

      monitor.events = [newEvent, ...monitor.events.slice(0, MAX_EVENT_HISTORY - 1)];
      monitor.stats = event.ns;
    }),
  );
};
