// Source: https://chakra-ui.com/docs/styled-system/color-mode

import { extendTheme } from "@chakra-ui/react";


const theme = extendTheme({
  initialColorMode: "system",
  useSystemColorMode: false,

  // Source: https://github.com/chakra-ui/chakra-ui/blob/main/packages/components/theme/src/foundations/colors.ts
  colors: {
    monitor: {
      up: "#48BB78",      // green.400
      warn: "#ECC94B",    // yellow.400
      down: "#F56565",    // red.400
      unknown: "#A0AEC0"  // gray.400
    }
  },
});

export default theme;
