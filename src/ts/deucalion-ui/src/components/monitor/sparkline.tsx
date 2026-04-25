import { type Component, createMemo, Show } from "solid-js";

interface SparklineProps {
  values: number[];
  height?: number;
  showDot?: boolean;
}

interface SparkData {
  linePath: string;
  fillPath: string;
  last: { x: number; y: number };
}

export const Sparkline: Component<SparklineProps> = (props) => {
  const data = createMemo<SparkData | null>(() => {
    const vals = props.values.filter((v) => Number.isFinite(v));
    if (vals.length < 2) return null;
    const min = Math.min(...vals);
    const max = Math.max(...vals);
    const range = max - min || 1;
    const w = 100;
    const h = 100;
    const pad = 4;
    const innerW = w - pad * 2;
    const innerH = h - pad * 2;
    const step = innerW / (vals.length - 1);
    const points = vals.map((v, i) => ({
      x: pad + i * step,
      y: pad + innerH - ((v - min) / range) * innerH,
    }));
    const linePath = points.map((p, i) => `${i === 0 ? "M" : "L"}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(" ");
    const fillPath = `${linePath} L${(w - pad).toString()} ${(h - pad).toString()} L${pad.toString()} ${(h - pad).toString()} Z`;
    return { linePath, fillPath, last: points[points.length - 1] };
  });

  return (
    <svg class="spark" viewBox="0 0 100 100" preserveAspectRatio="none" height={props.height ?? 26}>
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
