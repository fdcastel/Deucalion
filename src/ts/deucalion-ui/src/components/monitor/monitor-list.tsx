import { For, type Component, createMemo, Show } from "solid-js";

import { monitorList } from "../../stores/monitors-store";
import type { MonitorProps } from "../../services/deucalion-types";

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

export const MonitorList: Component = () => {
  const subgroups = createMemo(() => buildSubgroups(monitorList()));
  const showSubgroupHeader = (sg: SubGroup): boolean =>
    subgroups().length > 1 || sg.name !== NO_GROUP_LABEL;

  return (
    <section class="group">
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
