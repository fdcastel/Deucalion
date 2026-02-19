import { heroui } from "@heroui/react";
import plugin from "tailwindcss/plugin.js";
import { monitorThemeColors } from "./monitor-theme.js";

export default plugin(
  function ({ }) {
    // Plugin content (empty for now, just wrapping HeroUI)
  },
  // Plugin config
  {
    theme: {
      extend: {
        colors: {
          ...monitorThemeColors,
        },
      },
    },
  }
);
