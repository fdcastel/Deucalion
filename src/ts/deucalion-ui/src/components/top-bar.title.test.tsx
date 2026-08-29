import { render, waitFor } from "@solidjs/testing-library";
import { describe, expect, it, vi } from "vitest";

// `configuration` is a module-level resource that fetches once on import, so
// the page title must be stubbed before this file's imports are evaluated.
vi.hoisted(() => {
  vi.stubGlobal("fetch", vi.fn(async (): Promise<Response> =>
    new Response(JSON.stringify({ pageTitle: "Araponga *status* page" }), {
      status: 200,
      headers: { "content-type": "application/json" },
    }),
  ));
});

import { TopBar } from "./top-bar";

describe("<TopBar> title emphasis", () => {
  it("renders the *emphasis* fragment as <em> with plain head and tail", async () => {
    const { container } = render(() => <TopBar />);
    const brand = container.querySelector(".brand-name")!;

    await waitFor(() => { expect(brand.querySelector("em")).not.toBeNull(); });

    expect(brand.querySelector("em")?.textContent).toBe("status");
    expect(brand.textContent).toBe("Araponga status page");
    // document.title drops the markers entirely.
    expect(document.title).toBe("Araponga status page");
  });
});
