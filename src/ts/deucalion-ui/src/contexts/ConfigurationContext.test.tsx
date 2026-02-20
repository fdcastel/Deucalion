import { Suspense } from "react";
import { render, screen, waitFor } from "@testing-library/react";
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
    render(
      <SWRConfig value={{ provider: () => new Map() }}>
        <Suspense fallback={<div>loading</div>}>
          <ConfigurationProvider>
            <ConfigurationProbe />
          </ConfigurationProvider>
        </Suspense>
      </SWRConfig>
    );

    await waitFor(() => expect(screen.getByTestId("title")).toHaveTextContent("My Status"));
    expect(screen.getByTestId("error")).toHaveTextContent("none");
  });
});
