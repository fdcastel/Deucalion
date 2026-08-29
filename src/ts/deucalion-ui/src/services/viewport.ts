import { createSignal, type Accessor } from "solid-js";

// Heartbeat-strip length by viewport width. Measured with Playwright: at 1480px
// a 120-tick strip fits the row grid, at 1280px 90 does, below that 60.
// Shared by the strip (how many ticks to draw) and the monitors store (how many
// events to ask the API for -- see `?events=` on GET /api/monitors).
export const stripLenForWidth = (w: number): number => {
  if (w >= 1480) return 120;
  if (w >= 1280) return 90;
  return 60;
};

// One signal + one `resize` listener for the whole page. Attached lazily on
// first use so importing this module has no side effects (tests pin
// `window.innerWidth` before rendering, and SSR has no window).
let shared: Accessor<number> | undefined;

export const stripLen = (): Accessor<number> => {
  if (shared) return shared;
  if (typeof window === "undefined") return () => 60;
  const [len, setLen] = createSignal(stripLenForWidth(window.innerWidth));
  window.addEventListener("resize", () => { setLen(stripLenForWidth(window.innerWidth)); });
  shared = len;
  return len;
};
