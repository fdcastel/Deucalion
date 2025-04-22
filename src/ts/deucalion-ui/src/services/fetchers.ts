import { DeucalionOptions, MonitorProps, MonitorCheckedDto, MonitorEventDto } from "../models";

export const configurationFetcher = (url: string) =>
  fetch(url)
    .then((response) => response.json())
    .then((json) => json as DeucalionOptions | undefined)
    .catch(() => undefined);


export const monitorsFetcher = (url: string) =>
  fetch(url)
    .then((response) => response.json())
    .then((json) => json as MonitorProps[] | undefined)
    .then((arr) => (arr ? new Map(arr.map((x) => [x.name, x])) : undefined))
    .catch(() => undefined);

export const appendNewEvent = (monitors: Map<string, MonitorProps>, event: MonitorCheckedDto) => {
  const monitorName = event.n;
  const monitor = monitors.get(monitorName);

  if (monitor) {
    const existingEvents = monitor.events.filter((x) => x.at === event.at);
    if (existingEvents.length === 0) {
      const newMonitors = new Map(monitors);

      const newEvent = {
        at: event.at,
        st: event.st,
        ms: event.ms,
        te: event.te      
      } as MonitorEventDto;

      const sliced = monitor.events.slice(0, 59); // keep only the last 59 events (plus the new one = 60)

      const newMonitorProps = {
        name: monitorName,
        config: monitor.config,
        stats: event.ns,
        events: [newEvent, ...sliced],
      };

      newMonitors.set(monitorName, newMonitorProps);

      return newMonitors;
    }
  }

  return monitors;
};
