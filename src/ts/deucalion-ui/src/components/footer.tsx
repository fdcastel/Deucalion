import type { Component } from "solid-js";

import { monitorList } from "../stores/monitors-store";

export const Footer: Component = () => (
  <footer class="footer">
    <span><a class="footer-link" href="https://github.com/fdcastel/Deucalion" target="_blank" rel="noopener noreferrer"><em>Deucalion</em></a> · monitoring engine</span>
    <span>{monitorList().length.toString()} monitors</span>
  </footer>
);
