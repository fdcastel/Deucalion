import { createResource } from "solid-js";

import { API_CONFIGURATION_URL } from "../configuration";
import type { PageConfigurationDto } from "../services/deucalion-types";
import { fetchWithRetry } from "../services/fetch-with-retry";

const fetchConfiguration = async (): Promise<PageConfigurationDto> => {
  const response = await fetchWithRetry(API_CONFIGURATION_URL);
  return await response.json() as PageConfigurationDto;
};

export const [configuration] = createResource(fetchConfiguration);
