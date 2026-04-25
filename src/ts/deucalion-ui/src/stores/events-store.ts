import { createSignal } from "solid-js";
import { createStore, produce } from "solid-js/store";

import { MonitorState, type MonitorCheckedDto, type MonitorStateChangedDto } from "../services/deucalion-types";

const MAX_EVENTS = 40;

export interface FeedEvent {
  id: string;
  name: string;
  from: MonitorState;
  to: MonitorState;
  at: number;
  ms?: number;
}

interface EventsStoreState {
  items: FeedEvent[];
}

const [state, setState] = createStore<EventsStoreState>({ items: [] });
const [tick, setTick] = createSignal(0); // forces re-render of relative-time strings

// eslint-disable-next-line solid/reactivity
export const feedEvents = state;
export const feedTick = tick;
export const bumpFeedTick = (): void => { setTick((n) => n + 1); };

const pushEvent = (e: FeedEvent): void => {
  setState(
    produce((s) => {
      s.items = [e, ...s.items].slice(0, MAX_EVENTS);
    }),
  );
};

// Push from a MonitorChecked SSE event when state changes.
export const onMonitorChecked = (event: MonitorCheckedDto): void => {
  if (event.fr === event.st) return;
  pushEvent({
    id: `${event.n}-${event.at.toString()}-c`,
    name: event.n,
    from: event.fr,
    to: event.st,
    at: event.at,
    ms: event.ms,
  });
};

// Also accept MonitorStateChanged. The checked event will already have produced
// an entry, so de-dupe by id.
export const onMonitorStateChanged = (event: MonitorStateChangedDto): void => {
  const checkedId = `${event.n}-${event.at.toString()}-c`;
  setState(
    produce((s) => {
      if (s.items.some((x) => x.id === checkedId)) return;
      const next: FeedEvent = {
        id: `${event.n}-${event.at.toString()}-sc`,
        name: event.n,
        from: event.fr,
        to: event.st,
        at: event.at,
      };
      s.items = [next, ...s.items].slice(0, MAX_EVENTS);
    }),
  );
};
