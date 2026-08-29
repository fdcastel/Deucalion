import { For, type Component, type JSX } from "solid-js";

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
