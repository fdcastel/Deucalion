export const API_CONFIGURATION_URL = "/api/configuration";
export const API_MONITORS_URL = "/api/monitors";
export const API_EVENTS_URL = "/api/monitors/events";

// Must match Deucalion.Api.Application.EventHistoryCount — the backend serves
// up to this many events per monitor and the heartbeat strip / trend sparkline
// scale up to this length on wide viewports.
export const MAX_EVENT_HISTORY = 120;
