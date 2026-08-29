import type { MonitorDto, MonitorEventDto, MonitorEventsDto, MonitorWireDto } from "./deucalion-types";

// GET /api/monitors ships each monitor's events in columnar form (see
// MonitorEventsDto): a newest timestamp plus second deltas, one state digit
// per event, and a parallel latency array. Decoding back to one object per
// event keeps every component on the shape it already understands; only this
// boundary knows the wire format. Mirrors Deucalion.Api MonitorEventsDto.From.
export const decodeEvents = (wire: MonitorEventsDto | undefined): MonitorEventDto[] => {
  if (wire === undefined) return [];
  const count = wire.st.length;
  const events: MonitorEventDto[] = new Array<MonitorEventDto>(count);
  let at = wire.at;
  for (let i = 0; i < count; i++) {
    const ms = wire.ms[i];
    events[i] = ms == null
      ? { at, st: Number(wire.st[i]) }
      : { at, st: Number(wire.st[i]), ms };
    // dt has one entry fewer than st; the last iteration never reads past it.
    if (i < count - 1) at -= wire.dt[i];
  }
  return events;
};

export const decodeMonitor = (wire: MonitorWireDto): MonitorDto => {
  const { events, ...rest } = wire;
  return { ...rest, events: decodeEvents(events) };
};
