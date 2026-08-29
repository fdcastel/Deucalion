import { readdir, rm } from "node:fs/promises";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

// playwright.config.ts points the backend at ./.e2e-storage/<run id>, which
// `dotnet run` resolves against the project directory. Each run gets its own
// subdirectory, and teardown deletes every *other* run's directory: on Windows
// the backend is still alive (and holding the SQLite file) while teardown
// runs, so the current run's directory cannot be removed here -- the next run
// takes care of it. Without any of this the databases accumulated forever.
const storageRoot = fileURLToPath(new URL("../../../../cs/Deucalion.Api/.e2e-storage", import.meta.url));

export default async (): Promise<void> => {
  const current = process.env.E2E_STORAGE_RUN_ID;
  let entries: string[];
  try {
    entries = await readdir(storageRoot);
  } catch {
    return; // Nothing to clean.
  }

  for (const entry of entries) {
    if (entry === current) continue;
    try {
      await rm(join(storageRoot, entry), { recursive: true, force: true, maxRetries: 3, retryDelay: 250 });
    } catch (err) {
      // Best effort: a directory still locked by a lingering process is left
      // for the next run rather than failing an otherwise green e2e suite.
      console.warn(`e2e teardown: could not remove ${entry}:`, err);
    }
  }
};
