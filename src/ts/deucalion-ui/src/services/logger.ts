type LogLevelType = "debug" | "error" | "info" | "log" | "trace" | "warn";

// Console output is a development aid only: production builds ship silent
// (`import.meta.env.DEV` is statically false there, so the branch is
// dead-code-eliminated by vite).
const enabled: boolean = import.meta.env.DEV;

const writeLog = (logLevel: LogLevelType, ...data: unknown[]) => {
  if (enabled) {
    console[logLevel](...data);
  }
};

export const log = (...data: unknown[]): void => {
  writeLog("log", ...data);
};

export const warn = (...data: unknown[]): void => {
  writeLog("warn", ...data);
};

export const error = (...data: unknown[]): void => {
  writeLog("error", ...data);
};
