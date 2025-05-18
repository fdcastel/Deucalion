import { createSystem, defaultConfig } from "@chakra-ui/react";

export const system = createSystem(defaultConfig, {
  theme: {
    tokens: {
      colors: {
        // Source: https://chakra-ui.com/docs/theming/colors
        monitor: {
          unknown: { value: "#A0AEC0" },  // gray.400
          down: { value: "#F56565" },     // red.400
          up: { value: "#48BB78" },       // green.400
          warn: { value: "#ECC94B" },     // yellow.400
          degraded: { value: "#C6F6D5" }, // green.100
        },
      },
    },
    semanticTokens: {
      colors: {
        // Source: https://github.com/chakra-ui/chakra-ui/blob/main/packages/react/src/theme/semantic-tokens/colors.ts
        bg: {
          DEFAULT: {
            value: { _light: "{colors.white}", _dark: "#1c202b" /* From Chakra UI v2 */ },
          },
          subtle: {
            value: { _light: "{colors.gray.50}", _dark: "{colors.gray.950}" },
          },
          muted: {
            value: { _light: "{colors.gray.100}", _dark: "{colors.gray.900}" },
          },
          emphasized: {
            value: { _light: "{colors.gray.200}", _dark: "#1a1d28" /* From Chakra UI v2 */ },
          },
          inverted: {
            value: { _light: "{colors.black}", _dark: "{colors.white}" },
          },
          panel: {
            value: { _light: "{colors.white}", _dark: "#303747" /* From Chakra UI v2 */ },
          },
          error: {
            value: { _light: "{colors.red.50}", _dark: "{colors.red.950}" },
          },
          warning: {
            value: { _light: "{colors.orange.50}", _dark: "{colors.orange.950}" },
          },
          success: {
            value: { _light: "{colors.green.50}", _dark: "{colors.green.950}" },
          },
          info: {
            value: { _light: "{colors.blue.50}", _dark: "{colors.blue.950}" },
          },
        },
      },
    },
  },
});
