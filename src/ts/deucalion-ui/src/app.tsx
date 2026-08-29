import { createEffect, ErrorBoundary, onMount, Show, type Component } from "solid-js";

import { configuration } from "./stores/configuration-store";
import { monitorsLoaded, monitorsResource } from "./stores/monitors-store";

import { TopBar } from "./components/top-bar";
import { HeroAvailability } from "./components/hero/hero-availability";
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

// Everything that decides readiness lives *inside* the ErrorBoundary. Both
// resources are read here: a rejected resource only rethrows on read, so a
// fatal /api/monitors fetch that nothing reads would leave the splash up
// forever with no error surfaced (#17). Reading it from a computation owned
// by the boundary routes the rethrow to the fallback.
const Dashboard: Component = () => {
  const ready = (): boolean => Boolean(configuration() && monitorsResource() && monitorsLoaded());

  createEffect(() => {
    if (ready()) hideSplash();
  });

  return (
    <Show when={ready()}>
      <div class="shell">
        <TopBar />
        <div class="hero">
          <HeroAvailability />
        </div>
        <MonitorList />
        <Footer />
      </div>
    </Show>
  );
};

export const App: Component = () => (
  <ErrorBoundary fallback={(err: unknown) => <Crashed err={err} />}>
    <Dashboard />
    <ToastStack />
    <TweaksPanel />
  </ErrorBoundary>
);
