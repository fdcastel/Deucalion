import { ChakraProvider, ColorModeScript, ThemeConfig, extendTheme } from "@chakra-ui/react";

const theme = extendTheme({
  initialColorMode: "system",
  useSystemColorMode: false,

  // Source: https://v2.chakra-ui.com/docs/styled-system/theme#colors
  colors: {
    monitor: {
      unknown: "#A0AEC0",  // gray.400
      down: "#F56565",     // red.400
      up: "#48BB78",       // green.400
      warn: "#ECC94B",     // yellow.400
      degraded: "#C6F6D5", // green.100
    },
  },

  semanticTokens: {
    colors: {
      flash: {
        default: 'blue.100',
        _dark: 'blue.900',
      },
    },
  },
    
});

interface ChakraEnvironmentProps {
  children: React.ReactNode;
}

export const ChakraEnvironment = ({ children }: ChakraEnvironmentProps) => {
  return (
    <div>
      <ColorModeScript initialColorMode={(theme.config as ThemeConfig).initialColorMode} />
      <ChakraProvider theme={theme}>{children}</ChakraProvider>
    </div>
  );
};
