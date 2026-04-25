import { For, type Component, createMemo, onCleanup, onMount } from "solid-js";

import { feedEvents, feedTick, bumpFeedTick } from "../../stores/events-store";
import { fmtAgo, fmtMs, fmtTime, stateLabel, stateName } from "../../services/formatting";

const FEED_VISIBLE = 7;

export const EventFeed: Component = () => {
  onMount(() => {
    const id = setInterval(bumpFeedTick, 15_000);
    onCleanup(() => { clearInterval(id); });
  });

  const items = createMemo(() => feedEvents.items.slice(0, FEED_VISIBLE));

  return (
    <div class="feed">
      <div class="feed-header">
        <div class="feed-title">
          <span>Live events</span>
          <span class="feed-live">live</span>
        </div>
        <div class="feed-title">
          <span>{feedEvents.items.length.toString()} recent</span>
        </div>
      </div>
      <div class="feed-body">
        <ul class="feed-list">
          <For each={items()}>
            {(e) => {
              // Read feedTick to recompute fmtAgo every tick
              const ago = (): string => { feedTick(); return fmtAgo(e.at); };
              return (
                <li class="feed-item">
                  <span class="feed-time" title={fmtTime(e.at)}>{ago()}</span>
                  <div class="feed-msg">
                    <span class="feed-name">{e.name}</span>
                    <span class="feed-arrow">·</span>
                    <span class={`feed-state ${stateName(e.from)}`}>{stateLabel(e.from)}</span>
                    <span class="feed-arrow">→</span>
                    <span class={`feed-state ${stateName(e.to)}`}>{stateLabel(e.to)}</span>
                  </div>
                  <span class="feed-detail">{e.ms != null ? fmtMs(e.ms) : ""}</span>
                </li>
              );
            }}
          </For>
        </ul>
      </div>
    </div>
  );
};
