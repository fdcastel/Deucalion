import { createResource } from "solid-js";
import { createStore, produce } from "solid-js/store";

import { API_MONITORS_URL } from "../configuration";
import type {
  MonitorCheckedDto,
  MonitorEventDto,
  MonitorProps,
} from "../services/deucalion-types";

interface MonitorsStoreState {
  byName: Record<string, MonitorProps>;
  order: string[];
  loaded: boolean;
}

const [state, setState] = createStore<MonitorsStoreState>({
  byName: {},
  order: [],
  loaded: false,
});

const fetchMonitors = async (): Promise<MonitorProps[]> => {
  const response = await fetch(API_MONITORS_URL);
  if (!response.ok) throw new Error(`Failed to fetch monitors: ${response.status.toString()}`);
  return await response.json() as MonitorProps[];
};

const [monitorsResource] = createResource(async () => {
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
export { monitorsResource };
export const monitorsLoaded = (): boolean => state.loaded;

export const monitorList = (): MonitorProps[] => state.order.map((name) => state.byName[name]);

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
        te: event.te,
      };

      monitor.events = [newEvent, ...monitor.events.slice(0, 59)];
      monitor.stats = event.ns;
    }),
  );
};
