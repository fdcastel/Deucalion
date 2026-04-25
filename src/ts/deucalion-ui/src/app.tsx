import { createEffect, ErrorBoundary, onMount, Show, type Component } from "solid-js";

import { configuration } from "./stores/configuration-store";
import { monitorsLoaded } from "./stores/monitors-store";

import { TopBar } from "./components/top-bar";
import { Hero } from "./components/hero/hero";
import { MonitorList } from "./components/monitor/monitor-list";
import { Footer } from "./components/footer";
import { ToastStack } from "./components/common/toast";
import { TweaksPanel } from "./components/tweaks/tweaks-panel";

// Fade out the splash element from index.html and remove it. The splash IS
// the loading screen — we don't render a second one.
const hideSplash = (): void => {
  const splash = document.getElementById("splash");
  if (!splash) return;
  splash.classList.add("hidden");
  setTimeout(() => { splash.remove(); }, 400);
};

const Crashed: Component<{ err: unknown }> = (props) => {
  // Errors short-circuit the loading state too — make sure the splash
  // doesn't keep covering the page.
  onMount(hideSplash);
  return (
    <div class="shell">
      <h1 class="brand-name">Something went wrong</h1>
      <pre class="mono" style={{ color: "var(--down)", "white-space": "pre-wrap" }}>
        {String(props.err)}
      </pre>
    </div>
  );
};

export const App: Component = () => {
  const ready = (): boolean => Boolean(configuration() && monitorsLoaded());

  createEffect(() => {
    if (ready()) hideSplash();
  });

  return (
    <ErrorBoundary fallback={(err: unknown) => <Crashed err={err} />}>
      <Show when={ready()}>
        <div class="shell">
          <TopBar />
          <Hero />
          <MonitorList />
          <Footer />
        </div>
      </Show>
      <ToastStack />
      <TweaksPanel />
    </ErrorBoundary>
  );
};
