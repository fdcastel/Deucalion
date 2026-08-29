import { afterEach, describe, expect, it, vi } from "vitest";

// The logger reads `import.meta.env.DEV` at module load, so each case
// re-imports a fresh copy under the stubbed environment.
const loadLogger = async (dev: boolean): Promise<typeof import("./logger")> => {
  vi.resetModules();
  vi.stubEnv("DEV", dev);
  return await import("./logger");
};

describe("logger", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
  });

  it("writes to the console in development", async () => {
    const logger = await loadLogger(true);
    const logSpy = vi.spyOn(console, "log").mockImplementation(() => undefined);
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => undefined);

    logger.log("a", 1);
    logger.warn("b");
    logger.error("c", new Error("x"));

    expect(logSpy).toHaveBeenCalledWith("a", 1);
    expect(warnSpy).toHaveBeenCalledWith("b");
    expect(errorSpy).toHaveBeenCalledTimes(1);
  });

  it("ships silent in production builds (issue #27)", async () => {
    const logger = await loadLogger(false);
    const logSpy = vi.spyOn(console, "log").mockImplementation(() => undefined);
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => undefined);

    logger.log("a");
    logger.warn("b");
    logger.error("c");

    expect(logSpy).not.toHaveBeenCalled();
    expect(warnSpy).not.toHaveBeenCalled();
    expect(errorSpy).not.toHaveBeenCalled();
  });
});
