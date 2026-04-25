import { createSignal } from "solid-js";

import * as logger from "./logger";

// Connection phase, used by the loading screen and the SSE indicator.
//   "initial"  — first request in flight, no failure yet
//   "waiting"  — at least one transient failure; backend likely still booting
//   "ready"    — initial fetch succeeded
export type BackendPhase = "initial" | "waiting" | "ready";

const [phase, setPhase] = createSignal<BackendPhase>("initial");
export const backendPhase = phase;

const sleep = (ms: number): Promise<void> =>
  new Promise<void>((resolve) => setTimeout(resolve, ms));

// 250 ms, 500 ms, 1 s, 2 s, then capped at 5 s.
const backoff = (attempt: number): number => Math.min(5000, 250 * 2 ** Math.max(0, attempt - 1));

const isTransient = (status: number): boolean => status === 0 || status >= 500;

export interface FetchOptions {
  signal?: AbortSignal;
}

// Fetch a URL with infinite retry on transient failures (network errors,
// 502/503/504 from the Vite proxy when the backend is still booting). 4xx
// responses are treated as fatal and surface the error.
export const fetchWithRetry = async (url: string, opts: FetchOptions = {}): Promise<Response> => {
  let attempt = 0;
  for (;;) {
    attempt++;
    let res: Response | undefined;
    try {
      res = await fetch(url, { signal: opts.signal });
    } catch (e) {
      // AbortError propagates immediately — nothing to retry against.
      if (e instanceof DOMException && e.name === "AbortError") throw e;
      logger.warn(`[fetchWithRetry] ${url} threw; retrying (attempt ${attempt.toString()})`, e);
      setPhase("waiting");
      await sleep(backoff(attempt));
      continue;
    }
    if (res.ok) {
      setPhase("ready");
      return res;
    }
    if (!isTransient(res.status)) {
      // 4xx responses surface as a permanent error — the resource consumer
      // (Solid's createResource) will route them to <ErrorBoundary>.
      throw new Error(`HTTP ${res.status.toString()} ${res.statusText} for ${url}`);
    }
    logger.warn(`[fetchWithRetry] ${url} returned ${res.status.toString()}; retrying (attempt ${attempt.toString()})`);
    setPhase("waiting");
    await sleep(backoff(attempt));
  }
};

// Test-only reset of the phase signal.
export const __resetBackendPhaseForTests = (): void => { setPhase("initial"); };
