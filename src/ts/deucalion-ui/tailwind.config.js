import { heroui } from "@heroui/react";
import { monitorThemeColors, monitorStateSafelist } from "./monitor-theme.js";

export default {
  theme: {
    extend: {
      colors: {
        ...monitorThemeColors,
      },
    },
  },
  safelist: monitorStateSafelist,
  plugins: [heroui()],
}