import { rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";

// playwright.config.ts points the backend at ./.e2e-storage, which `dotnet run`
// resolves against the project directory. The SQLite database (and its WAL)
// would otherwise accumulate across runs; nothing else cleans it up.
const storageDir = fileURLToPath(new URL("../../../../cs/Deucalion.Api/.e2e-storage", import.meta.url));

export default async (): Promise<void> => {
  await rm(storageDir, { recursive: true, force: true });
};
