import { Flex, Spacer, Text } from "@chakra-ui/react";

import { DeucalionIconComponent } from "./deucalion-icon";
import { ThemeSwitcher } from "./theme-switcher";

interface HeaderProps {
  title: string;
}

export const Header = ({ title }: HeaderProps) => (
  <Flex>
    <DeucalionIconComponent width="3em" height="3em" />
    <Text marginLeft="0.25em" fontSize="3xl" noOfLines={1}>
      {title}
    </Text>
    <Spacer />
    <ThemeSwitcher />
  </Flex>
);
