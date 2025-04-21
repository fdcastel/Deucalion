import * as React from "react";

import { useColorMode, IconButton, IconButtonProps, Icon } from "@chakra-ui/react";
import { MdDarkMode, MdLightMode } from "react-icons/md";

type ThemeSwitcherProps = Omit<IconButtonProps, "aria-label">;

export const ThemeSwitcher: React.FC<ThemeSwitcherProps> = (props) => {
  const { colorMode, toggleColorMode } = useColorMode();

  return (
    <IconButton
      variant="ghost"
      onClick={toggleColorMode}
      icon={
        <Icon
          as={
            colorMode === "light" ? MdDarkMode : MdLightMode
          }
        />
      }
      aria-label={`Switch to ${colorMode} mode`}
      {...props}
    />
  );
};
