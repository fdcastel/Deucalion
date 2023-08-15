import * as React from "react";

import { useColorMode, IconButton, IconButtonProps, Icon } from "@chakra-ui/react";

import { MdDarkMode, MdLightMode } from "react-icons/md";

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
            colorMode === "light" ? MdDarkMode : MdLightMode
          }
        />
      }
      aria-label={`Switch to ${colorMode} mode`}
      {...props}
    />
  );
};

