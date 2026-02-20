export const API_CONFIGURATION_URL = "/api/configuration";
export const API_MONITORS_URL = "/api/monitors";
export const API_HUB_URL = "/api/monitors/hub";

export const SWR_OPTIONS = {
	// React 19 + Vitest/JSDOM can report "uncached promise" warnings for SWR suspense.
	// Keep suspense for app runtime, but disable it in tests to keep CI output clean.
	suspense: import.meta.env.MODE !== "test",
	revalidateOnMount: true,
	revalidateOnFocus: false,
	revalidateOnReconnect: false,
};
