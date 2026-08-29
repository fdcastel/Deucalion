import { createEffect, createResource } from "solid-js";
import { createStore, produce } from "solid-js/store";

import { API_MONITORS_URL, MAX_EVENT_HISTORY } from "../configuration";
import type {
  MonitorCheckedDto,
  MonitorEventDto,
  MonitorDto,
  MonitorWireDto,
} from "../services/deucalion-types";
import { fetchWithRetry } from "../services/fetch-with-retry";
import { stripLen } from "../services/viewport";
import { decodeMonitor } from "../services/wire";

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

// Ask for as many events as the heartbeat strip can show at this viewport: the
// event lists are most of the payload, and a phone never draws more than 60.
// `loadedEventCount` remembers what was asked for, so growing the viewport past
// it triggers a refetch (below) instead of leaving the wider strip half empty.
let loadedEventCount = 0;

const fetchMonitors = async (): Promise<MonitorDto[]> => {
  const count = stripLen()();
  const response = await fetchWithRetry(`${API_MONITORS_URL}?events=${count.toString()}`);
  loadedEventCount = count;
  return (await response.json() as MonitorWireDto[]).map(decodeMonitor);
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

// Resized into a wider tier than the initial fetch covered (phone -> desktop,
// window un-snapped): fetch the longer history once. Never re-fetches on
// shrink -- the strip simply shows fewer of the events already loaded.
createEffect(() => {
  const wanted = stripLen()();
  if (state.loaded && wanted > loadedEventCount) refetchMonitors();
});

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
