import { type Component, createMemo, Show } from "solid-js";

interface SparklineProps {
  values: number[];
  max?: number;
  height?: number;
  showDot?: boolean;
}

interface SparkData {
  linePath: string;
  fillPath: string;
  last: { x: number; y: number };
  warnY: number | null;
}

// Anything below this min-spread (in ms) is treated as "flat" when auto-scaling.
// Stops 0/1ms oscillation from filling the box on tight monitors that have no max.
const NOISE_FLOOR_MS = 5;

export const Sparkline: Component<SparklineProps> = (props) => {
  const data = createMemo<SparkData | null>(() => {
    const vals = props.values.filter((v) => Number.isFinite(v));
    if (vals.length < 2) return null;
    const w = 100;
    const h = 100;
    const pad = 4;
    const innerW = w - pad * 2;
    const innerH = h - pad * 2;
    const step = innerW / (vals.length - 1);

    const maxProp = props.max;
    let yOf: (v: number) => number;
    let warnY: number | null = null;
    if (typeof maxProp === "number" && maxProp > 0) {
      // Anchored at 0..max so a tight monitor reads as "near baseline"
      // and a slow probe reads as "near WARN".
      const top = Math.max(maxProp, NOISE_FLOOR_MS);
      // Faint line at the WARN threshold. Coincides with chart top in the
      // common case; sits inside the chart when the floor lifted the ceiling.
      warnY = pad + innerH - (maxProp / top) * innerH;
      yOf = (v) => {
        const clamped = v < 0 ? 0 : v > top ? top : v;
        return pad + innerH - (clamped / top) * innerH;
      };
    } else {
      // Fallback: auto-scale to the data range, but apply a noise floor
      // so a 0/1ms jitter doesn't fill the whole box.
      const min = Math.min(...vals);
      const max = Math.max(...vals);
      const rawRange = max - min;
      const range = rawRange < NOISE_FLOOR_MS ? NOISE_FLOOR_MS : rawRange;
      yOf = (v) => pad + innerH - ((v - min) / range) * innerH;
    }

    const points = vals.map((v, i) => ({
      x: pad + i * step,
      y: yOf(v),
    }));
    const linePath = points.map((p, i) => `${i === 0 ? "M" : "L"}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(" ");
    const fillPath = `${linePath} L${(w - pad).toString()} ${(h - pad).toString()} L${pad.toString()} ${(h - pad).toString()} Z`;
    return { linePath, fillPath, last: points[points.length - 1], warnY };
  });

  return (
    <svg class="spark" viewBox="0 0 100 100" preserveAspectRatio="none" height={props.height ?? 26}>
      <Show when={data()}>
        {(d) => (
          <>
            <Show when={d().warnY}>
              {(wy) => <line class="spark-warn" x1="0" x2="100" y1={wy()} y2={wy()} />}
            </Show>
            <path class="spark-fill" d={d().fillPath} />
            <path class="spark-line" d={d().linePath} />
            <Show when={props.showDot !== false}>
              <circle class="spark-dot" cx={d().last.x} cy={d().last.y} r="1.6" />
            </Show>
          </>
        )}
      </Show>
    </svg>
  );
};
