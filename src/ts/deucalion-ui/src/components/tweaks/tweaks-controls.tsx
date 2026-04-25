import { For, type Component, type JSX, createSignal } from "solid-js";

interface TweakSectionProps {
  label: string;
  children: JSX.Element;
}

export const TweakSection: Component<TweakSectionProps> = (props) => (
  <>
    <div class="twk-sect">{props.label}</div>
    {props.children}
  </>
);

interface TweakRowProps {
  label: string;
  value?: string;
  inline?: boolean;
  children: JSX.Element;
}

const TweakRow: Component<TweakRowProps> = (props) => (
  <div class={props.inline ? "twk-row twk-row-h" : "twk-row"}>
    <div class="twk-lbl">
      <span>{props.label}</span>
      {props.value != null && <span class="twk-val">{props.value}</span>}
    </div>
    {props.children}
  </div>
);

interface TweakSliderProps {
  label: string;
  value: number;
  min?: number;
  max?: number;
  step?: number;
  unit?: string;
  onChange: (v: number) => void;
}

export const TweakSlider: Component<TweakSliderProps> = (props) => (
  <TweakRow label={props.label} value={`${props.value.toString()}${props.unit ?? ""}`}>
    <input
      type="range"
      class="twk-slider"
      min={props.min ?? 0}
      max={props.max ?? 100}
      step={props.step ?? 1}
      value={props.value}
      onInput={(e) => { props.onChange(Number(e.currentTarget.value)); }}
    />
  </TweakRow>
);

interface RadioOption { value: string; label: string }

interface TweakRadioProps {
  label: string;
  value: string;
  options: RadioOption[];
  onChange: (v: string) => void;
}

export const TweakRadio: Component<TweakRadioProps> = (props) => {
  let trackRef!: HTMLDivElement;
  const [dragging, setDragging] = createSignal(false);
  const idx = (): number => {
    const i = props.options.findIndex((o) => o.value === props.value);
    return i < 0 ? 0 : i;
  };
  const n = (): number => props.options.length;

  const segAt = (clientX: number): string => {
    const r = trackRef.getBoundingClientRect();
    const inner = r.width - 4;
    let i = Math.floor(((clientX - r.left - 2) / inner) * n());
    if (i < 0) i = 0;
    if (i >= n()) i = n() - 1;
    return props.options[i].value;
  };

  const onPointerDown = (e: PointerEvent): void => {
    setDragging(true);
    const v0 = segAt(e.clientX);
    if (v0 !== props.value) props.onChange(v0);
    const move = (ev: PointerEvent): void => {
      const v = segAt(ev.clientX);
      if (v !== props.value) props.onChange(v);
    };
    const up = (): void => {
      setDragging(false);
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
    };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up);
  };

  return (
    <TweakRow label={props.label}>
      <div
        ref={trackRef}
        role="radiogroup"
        onPointerDown={onPointerDown}
        class={dragging() ? "twk-seg dragging" : "twk-seg"}
      >
        <div
          class="twk-seg-thumb"
          style={{
            left: `calc(2px + ${idx().toString()} * (100% - 4px) / ${n().toString()})`,
            width: `calc((100% - 4px) / ${n().toString()})`,
          }}
        />
        <For each={props.options}>
          {(o) => (
            <button type="button" role="radio" aria-checked={o.value === props.value}>
              {o.label}
            </button>
          )}
        </For>
      </div>
    </TweakRow>
  );
};

interface TweakSelectProps {
  label: string;
  value: string;
  options: RadioOption[];
  onChange: (v: string) => void;
}

export const TweakSelect: Component<TweakSelectProps> = (props) => (
  <TweakRow label={props.label}>
    <select
      class="twk-field"
      value={props.value}
      onChange={(e) => { props.onChange(e.currentTarget.value); }}
    >
      <For each={props.options}>
        {(o) => <option value={o.value}>{o.label}</option>}
      </For>
    </select>
  </TweakRow>
);
