type LogLevelType = "debug" | "error" | "info" | "log" | "trace" | "warn";

let enabled = true;

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

export const disableLogger = (): void => {
  enabled = false;
};
