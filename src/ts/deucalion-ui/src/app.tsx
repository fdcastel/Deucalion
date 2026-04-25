import { ErrorBoundary, Show, type Component } from "solid-js";

import { configuration } from "./stores/configuration-store";
import { monitorsLoaded } from "./stores/monitors-store";
import { backendPhase } from "./services/fetch-with-retry";

import { TopBar } from "./components/top-bar";
import { Hero } from "./components/hero/hero";
import { MonitorList } from "./components/monitor/monitor-list";
import { Footer } from "./components/footer";
import { ToastStack } from "./components/common/toast";
import { TweaksPanel } from "./components/tweaks/tweaks-panel";

const Loading: Component = () => (
  <div class="shell" style={{ "min-height": "100vh", display: "grid", "place-items": "center" }}>
    <div style={{ display: "flex", "flex-direction": "column", "align-items": "center", gap: "10px" }}>
      <span class="connection-dot connecting" aria-hidden="true" />
      <div class="hero-label">
        {backendPhase() === "waiting" ? "Waiting for backend…" : "Loading…"}
      </div>
    </div>
  </div>
);

const Crashed: Component<{ err: unknown }> = (props) => (
  <div class="shell">
    <h1 class="brand-name">Something went wrong</h1>
    <pre class="mono" style={{ color: "var(--down)", "white-space": "pre-wrap" }}>
      {String(props.err)}
    </pre>
  </div>
);

export const App: Component = () => (
  <ErrorBoundary fallback={(err: unknown) => <Crashed err={err} />}>
    <Show when={configuration() && monitorsLoaded()} fallback={<Loading />}>
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
