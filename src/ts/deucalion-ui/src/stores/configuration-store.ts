import { createResource } from "solid-js";

import { API_CONFIGURATION_URL } from "../configuration";
import type { PageConfigurationDto } from "../services/deucalion-types";

const fetchConfiguration = async (): Promise<PageConfigurationDto> => {
  const response = await fetch(API_CONFIGURATION_URL);
  if (!response.ok) throw new Error(`Failed to fetch configuration: ${response.status.toString()}`);
  return await response.json() as PageConfigurationDto;
};

export const [configuration] = createResource(fetchConfiguration);
