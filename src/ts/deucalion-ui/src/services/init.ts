import { mutate } from "swr";
import type { MonitorProps, DeucalionOptions } from "./deucalion-types";
import { API_INIT_URL, API_CONFIGURATION_URL, API_MONITORS_URL } from "../configuration";

interface InitResponse {
  configuration: DeucalionOptions;
  monitors: MonitorProps[];
}

/**
 * Preload both configuration and monitors data in a single HTTP request.
 * Seeds the SWR cache for both keys so useSWR hooks find data immediately on mount.
 */
export const preloadInit = () => {
  void fetch(API_INIT_URL)
    .then(response => {
      if (!response.ok) throw new Error(`Failed to fetch init: ${response.status}`);
      return response.json() as Promise<InitResponse>;
    })
    .then(data => {
      void mutate(API_CONFIGURATION_URL, data.configuration, { revalidate: false });
      void mutate(API_MONITORS_URL, new Map(data.monitors.map(x => [x.name, x])), { revalidate: false });
    });
};
