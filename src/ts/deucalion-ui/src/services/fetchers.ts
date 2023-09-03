import { MonitorCheckedDto, MonitorState, MonitorProps, DeucalionOptions } from "../models";

const addStats = (m: MonitorProps) => {
  if (m.events.length > 0) {
    const unknownEventCount = m.events.reduce((acc, e) => acc + (e.st == MonitorState.Unknown ? 1 : 0), 0);
    const downEventCount = m.events.reduce((acc, e) => acc + (e.st == MonitorState.Down ? 1 : 0), 0);
    const averageResponseTimes = [...m.events].filter((e) => e.st > MonitorState.Down);

    m.stats = {
      availability: (100 * (m.events.length - downEventCount)) / (m.events.length - unknownEventCount),
      averageResponseTime: averageResponseTimes.length > 0 ? averageResponseTimes.reduce((acc, e) => acc + (e.ms ?? 0), 0) / averageResponseTimes.length : 0,
      lastState: m.events[m.events.length - 1].st,
      lastUpdate: m.events[m.events.length - 1].at,
    };
  }

  return m;
};

export const configurationFetcher = (url: string) =>
  fetch(url)
    .then((response) => response.json())
    .then((json) => json as DeucalionOptions | undefined)
    .catch(() => undefined);


export const monitorsFetcher = (url: string) =>
  fetch(url)
    .then((response) => response.json())
    .then((json) => json as MonitorProps[] | undefined)
    .then((arr) => (arr ? new Map(arr.map((x) => [x.name, addStats(x)])) : undefined))
    .catch(() => undefined);

export const appendNewEvent = (monitors: Map<string, MonitorProps>, newEvent: MonitorCheckedDto) => {
  const monitorName = newEvent.n ?? "";
  const monitor = monitors.get(monitorName);

  if (monitor) {
    const existingEvents = monitor.events.filter((x) => x.at === newEvent.at);
    if (existingEvents.length === 0) {
      const newMonitors = new Map(monitors);

      const newMonitorProps = addStats({
        name: monitorName,
        info: monitor.info,
        events: [...monitor.events, newEvent].slice(-60), // keep only the last 60
      });

      newMonitors.set(monitorName, newMonitorProps);

      return newMonitors;
    }
  }

  return monitors;
};
