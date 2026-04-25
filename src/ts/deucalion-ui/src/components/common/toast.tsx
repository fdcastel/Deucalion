import { For, type Component } from "solid-js";
import { Portal } from "solid-js/web";

import { toastList } from "../../stores/toast-store";

export const ToastStack: Component = () => (
  <Portal>
    <div class="toast-stack" role="status" aria-live="polite">
      <For each={toastList()}>
        {(t) => (
          <div class={`toast ${t.variant}`}>
            <div class="toast-title">{t.title}</div>
            {t.description && <div class="toast-desc">{t.description}</div>}
          </div>
        )}
      </For>
    </div>
  </Portal>
);
