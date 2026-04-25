import type { Component } from "solid-js";

import { monitorList } from "../stores/monitors-store";

export const Footer: Component = () => (
  <footer class="footer">
    <span><em>Deucalion</em> · monitoring engine</span>
    <span>{monitorList().length.toString()} monitors · 60 ticks</span>
  </footer>
);
