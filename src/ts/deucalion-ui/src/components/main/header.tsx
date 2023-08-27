import { Flex, Image, Spacer, Text } from "@chakra-ui/react";

import { ThemeSwitcher } from "./theme-switcher";

interface HeaderProps {
  title: string;
}

export const Header = ({ title }: HeaderProps) => (
  <Flex>
    <Image src="/assets/deucalion-icon.svg" width="3em" height="3em" marginRight="0.5em" alt="icon" />
    <Text fontSize="3xl" noOfLines={1}>
      {title}
    </Text>
    <Spacer />
    <ThemeSwitcher />
  </Flex>
);
