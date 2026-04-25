import type { Component } from "solid-js";

import { HeroAvailability } from "./hero-availability";
import { EventFeed } from "./event-feed";

export const Hero: Component = () => (
  <div class="hero">
    <HeroAvailability />
    <EventFeed />
  </div>
);
