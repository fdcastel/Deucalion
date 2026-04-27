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
}

// Anything below this min-spread (in ms) is treated as "flat" when auto-scaling.
// Stops 0/1ms oscillation from filling the box on tight monitors that have no max.
export const SPARKLINE_NOISE_FLOOR_MS = 5;
const NOISE_FLOOR_MS = SPARKLINE_NOISE_FLOOR_MS;

const W = 100;
const H = 100;
const PAD = 4;
const INNER_W = W - PAD * 2;
const INNER_H = H - PAD * 2;

export const Sparkline: Component<SparklineProps> = (props) => {
  // WARN reference line: depends only on `max`, not on having data points.
  // Renders even on all-Down monitors so the threshold context survives.
  const warnY = createMemo<number | null>(() => {
    const maxProp = props.max;
    if (typeof maxProp !== "number" || maxProp <= 0) return null;
    const top = Math.max(maxProp, NOISE_FLOOR_MS);
    return PAD + INNER_H - (maxProp / top) * INNER_H;
  });

  const data = createMemo<SparkData | null>(() => {
    const vals = props.values.filter((v) => Number.isFinite(v));
    if (vals.length < 2) return null;
    const step = INNER_W / (vals.length - 1);

    const maxProp = props.max;
    let yOf: (v: number) => number;
    if (typeof maxProp === "number" && maxProp > 0) {
      // Anchored at 0..max so a tight monitor reads as "near baseline"
      // and a slow probe reads as "near WARN".
      const top = Math.max(maxProp, NOISE_FLOOR_MS);
      yOf = (v) => {
        const clamped = v < 0 ? 0 : v > top ? top : v;
        return PAD + INNER_H - (clamped / top) * INNER_H;
      };
    } else {
      // Fallback: auto-scale to the data range, but apply a noise floor
      // so a 0/1ms jitter doesn't fill the whole box.
      const min = Math.min(...vals);
      const max = Math.max(...vals);
      const rawRange = max - min;
      const range = rawRange < NOISE_FLOOR_MS ? NOISE_FLOOR_MS : rawRange;
      yOf = (v) => PAD + INNER_H - ((v - min) / range) * INNER_H;
    }

    const points = vals.map((v, i) => ({
      x: PAD + i * step,
      y: yOf(v),
    }));
    const linePath = points.map((p, i) => `${i === 0 ? "M" : "L"}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(" ");
    const fillPath = `${linePath} L${(W - PAD).toString()} ${(H - PAD).toString()} L${PAD.toString()} ${(H - PAD).toString()} Z`;
    return { linePath, fillPath, last: points[points.length - 1] };
  });

  return (
    <svg class="spark" viewBox="0 0 100 100" preserveAspectRatio="none" height={props.height ?? 26}>
      <Show when={warnY()}>
        {(wy) => <line class="spark-warn" x1="0" x2="100" y1={wy()} y2={wy()} />}
      </Show>
      <Show when={data()}>
        {(d) => (
          <>
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
