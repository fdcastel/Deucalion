import { heroui } from "@heroui/react";
import plugin from "tailwindcss/plugin.js";

export default plugin(
  function ({ }) {
    // Plugin content (empty for now, just wrapping HeroUI)
  },
  // Plugin config
  {
    theme: {
      extend: {
        colors: {
          'monitor-unknown': 'var(--color-monitor-unknown)',
          'monitor-down': 'var(--color-monitor-down)',
          'monitor-up': 'var(--color-monitor-up)',
          'monitor-warn': 'var(--color-monitor-warn)',
          'monitor-degraded': 'var(--color-monitor-degraded)',
          'flash-light': 'var(--color-flash-light)',
          'flash-dark': 'var(--color-flash-dark)',
        },
      },
    },
  }
);
