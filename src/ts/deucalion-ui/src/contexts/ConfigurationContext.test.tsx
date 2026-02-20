import { Suspense } from "react";
import { act, render, screen } from "@testing-library/react";
import { SWRConfig } from "swr";
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";

import { ConfigurationProvider, useConfiguration } from "./ConfigurationContext";

const ConfigurationProbe = () => {
  const { configurationData, configurationError } = useConfiguration();
  return (
    <>
      <div data-testid="title">{configurationData?.pageTitle}</div>
      <div data-testid="error">{configurationError ? "error" : "none"}</div>
    </>
  );
};

describe("ConfigurationProvider", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ pageTitle: "My Status" }),
    }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders configuration through context", async () => {
    await act(async () => {
      render(
        <SWRConfig value={{ provider: () => new Map() }}>
          <Suspense fallback={<div>loading</div>}>
            <ConfigurationProvider>
              <ConfigurationProbe />
            </ConfigurationProvider>
          </Suspense>
        </SWRConfig>
      );
    });

    expect(await screen.findByTestId("title")).toHaveTextContent("My Status");
    expect(screen.getByTestId("error")).toHaveTextContent("none");
  });
});
