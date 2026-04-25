import { For, type Component, createMemo, Show } from "solid-js";

import { configuration } from "../../stores/configuration-store";
import { monitorList } from "../../stores/monitors-store";
import { MonitorState, type MonitorProps } from "../../services/deucalion-types";

import { MonitorRow } from "./monitor-row";

interface SubGroup {
  name: string;
  monitors: MonitorProps[];
}

const NO_GROUP_LABEL = "Monitors";

const buildSubgroups = (monitors: MonitorProps[]): SubGroup[] => {
  const map = new Map<string, MonitorProps[]>();
  for (const m of monitors) {
    const key = m.config.group ?? NO_GROUP_LABEL;
    const list = map.get(key);
    if (list) list.push(m);
    else map.set(key, [m]);
  }
  return [...map.entries()].map(([name, items]) => ({ name, monitors: items }));
};

const tally = (monitors: MonitorProps[]): { up: number; warn: number; down: number } => {
  let up = 0, warn = 0, down = 0;
  for (const m of monitors) {
    const s = m.stats?.lastState ?? m.events[0]?.st;
    if (s === MonitorState.Up) up++;
    else if (s === MonitorState.Warn) warn++;
    else if (s === MonitorState.Down || s === MonitorState.Degraded) down++;
  }
  return { up, warn, down };
};

export const MonitorList: Component = () => {
  const subgroups = createMemo(() => buildSubgroups(monitorList()));
  const totals = createMemo(() => tally(monitorList()));
  const showSubgroupHeader = (sg: SubGroup): boolean =>
    subgroups().length > 1 || sg.name !== NO_GROUP_LABEL;

  return (
    <section class="group">
      <div class="group-header">
        <h2 class="group-title">
          {configuration()?.pageTitle ?? "Status"} <em>· {monitorList().length.toString()} monitors</em>
        </h2>
        <div class="group-meta tnum">
          <span class="group-meta-up">● {totals().up.toString()} up</span>
          <Show when={totals().warn > 0}>
            <span class="group-meta-warn">● {totals().warn.toString()} warn</span>
          </Show>
          <Show when={totals().down > 0}>
            <span class="group-meta-down">● {totals().down.toString()} down</span>
          </Show>
        </div>
      </div>
      <For each={subgroups()}>
        {(sg) => (
          <div>
            <Show when={showSubgroupHeader(sg)}>
              <div class="subgroup">{sg.name}</div>
            </Show>
            <div>
              <For each={sg.monitors}>
                {(m) => <MonitorRow monitor={m} />}
              </For>
            </div>
          </div>
        )}
      </For>
    </section>
  );
};
