import * as React from "react";

import { useColorMode, IconButton, IconButtonProps, Icon } from "@chakra-ui/react";

import { FaMoon, FaSun } from "react-icons/fa";

type ThemeSwitcherComponentProps = Omit<IconButtonProps, "aria-label">;

export const ThemeSwitcherComponent: React.FC<ThemeSwitcherComponentProps> = (props) => {
  const { colorMode, toggleColorMode } = useColorMode();

  return (
    <IconButton
      size="md"
      fontSize="lg"
      variant="ghost"
      color="current"
      marginLeft="2"
      onClick={toggleColorMode}
      icon={
        <Icon
          as={
            // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment -- Reason: Crazy compiler.
            colorMode === "light" ? FaMoon : FaSun
          }
        />
      }
      aria-label={`Switch to ${colorMode} mode`}
      {...props}
    />
  );
};
