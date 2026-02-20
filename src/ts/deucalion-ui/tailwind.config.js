import { heroui } from "@heroui/theme";
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
  plugins: [heroui({ addCommonColors: false })],
}