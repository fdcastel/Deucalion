export const API_CONFIGURATION_URL = "/api/configuration";
export const API_MONITORS_URL = "/api/monitors";
export const API_EVENTS_URL = "/api/monitors/events";

// Must match Deucalion.Api.Application.EventHistoryCount — the most events the
// backend serves per monitor (`?events=` caps lower, never higher) and the
// length the heartbeat strip / trend sparkline scale up to on wide viewports.
export const MAX_EVENT_HISTORY = 120;
