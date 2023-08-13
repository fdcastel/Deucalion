// Source: https://chakra-ui.com/docs/styled-system/color-mode

import { extendTheme, type ThemeConfig } from '@chakra-ui/react'

const config: ThemeConfig = {
    initialColorMode: 'system',
    useSystemColorMode: false,
}

const theme = extendTheme({ config })

export default theme
