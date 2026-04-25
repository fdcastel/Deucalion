import { createSignal } from "solid-js";

import type { MonitorStateChangedDto } from "../services/deucalion-types";
import { monitorStateToDescription, monitorStateToToastVariant, type ToastVariant } from "../services/formatting";

export interface Toast {
  id: number;
  title: string;
  description?: string;
  variant: ToastVariant;
}

const [toasts, setToasts] = createSignal<Toast[]>([]);
export const toastList = toasts;

let nextId = 1;
const TOAST_TTL_MS = 4000;

export const showToast = (t: Omit<Toast, "id">): void => {
  const id = nextId++;
  setToasts((prev) => [...prev, { ...t, id }]);
  setTimeout(() => {
    setToasts((prev) => prev.filter((x) => x.id !== id));
  }, TOAST_TTL_MS);
};

export const showStateChangeToast = (event: MonitorStateChangedDto): void => {
  showToast({
    title: event.n,
    description: monitorStateToDescription(event.st),
    variant: monitorStateToToastVariant(event.st),
  });
};
